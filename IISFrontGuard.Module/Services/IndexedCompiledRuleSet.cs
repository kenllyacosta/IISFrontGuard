using IISFrontGuard.Module.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Indexes compiled WAF rules by cheap discriminators for fast candidate filtering.
    /// This dramatically improves performance when there are many rules.
    /// Inspired by Cloudflare's rule engine architecture.
    /// </summary>
    public class IndexedCompiledRuleSet
    {
        // Index by HTTP method
        private readonly Dictionary<string, List<CompiledRule>> _methodIndex;
        
        // Index by path prefix (first segment)
        private readonly Dictionary<string, List<CompiledRule>> _pathPrefixIndex;
        
        // Rules that require full evaluation (no discriminators)
        private readonly List<CompiledRule> _genericRules;
        
        // All rules (fallback)
        private readonly List<CompiledRule> _allRules;

        /// <summary>
        /// Initializes a new indexed compiled rule set.
        /// </summary>
        /// <param name="rules"></param>
        public IndexedCompiledRuleSet(IEnumerable<CompiledRule> rules)
        {
            _allRules = rules?.ToList() ?? new List<CompiledRule>();
            _methodIndex = new Dictionary<string, List<CompiledRule>>(StringComparer.OrdinalIgnoreCase);
            _pathPrefixIndex = new Dictionary<string, List<CompiledRule>>(StringComparer.OrdinalIgnoreCase);
            _genericRules = new List<CompiledRule>();

            BuildIndexes();
        }

        /// <summary>
        /// Builds indexes based on rule conditions.
        /// Analyzes each rule to determine which discriminators it uses.
        /// </summary>
        private void BuildIndexes()
        {
            foreach (var rule in _allRules)
            {
                var discriminators = ExtractDiscriminators(rule);

                IndexRuleByMethod(rule, discriminators.Methods);
                IndexRuleByPath(rule, discriminators.PathPrefixes);
                ClassifyGenericRule(rule, discriminators);
            }
        }

        /// <summary>
        /// Indexes a rule by its HTTP method discriminators.
        /// </summary>
        private void IndexRuleByMethod(CompiledRule rule, HashSet<string> methods)
        {
            if (methods == null || methods.Count == 0)
                return;

            foreach (var method in methods)
            {
                if (!_methodIndex.ContainsKey(method))
                    _methodIndex[method] = new List<CompiledRule>();

                _methodIndex[method].Add(rule);
            }
        }

        /// <summary>
        /// Indexes a rule by its path prefix discriminators.
        /// </summary>
        private void IndexRuleByPath(CompiledRule rule, HashSet<string> pathPrefixes)
        {
            if (pathPrefixes == null || pathPrefixes.Count == 0)
                return;

            foreach (var prefix in pathPrefixes)
            {
                if (!_pathPrefixIndex.ContainsKey(prefix))
                    _pathPrefixIndex[prefix] = new List<CompiledRule>();

                _pathPrefixIndex[prefix].Add(rule);
            }
        }

        /// <summary>
        /// Classifies a rule as generic if it has no discriminators.
        /// Generic rules are always evaluated.
        /// </summary>
        private void ClassifyGenericRule(CompiledRule rule, RuleDiscriminators discriminators)
        {
            var hasMethodDiscriminators = discriminators.Methods != null && discriminators.Methods.Count > 0;
            var hasPathDiscriminators = discriminators.PathPrefixes != null && discriminators.PathPrefixes.Count > 0;

            if (!hasMethodDiscriminators && !hasPathDiscriminators)
            {
                _genericRules.Add(rule);
            }
        }

        /// <summary>
        /// Gets candidate rules that might match the given request context.
        /// This is much faster than evaluating all rules.
        /// </summary>
        public IEnumerable<CompiledRule> GetCandidateRules(RequestContext context)
        {
            var candidates = new HashSet<CompiledRule>();

            // Always include generic rules (no discriminators)
            candidates.UnionWith(_genericRules);

            // Add rules matching method
            if (_methodIndex.TryGetValue(context.Method, out var methodRules))
                candidates.UnionWith(methodRules);

            // Add rules matching path prefix
            var pathPrefix = GetPathPrefix(context.Path);
            if (!string.IsNullOrEmpty(pathPrefix) && _pathPrefixIndex.TryGetValue(pathPrefix, out var pathRules))
                candidates.UnionWith(pathRules);

            // If we found candidates via indexing, return them
            if (candidates.Count > 0)
                return candidates.OrderBy(r => r.Priority);

            // Fallback: return all rules if no index matches
            return _allRules;
        }

        /// <summary>
        /// Extracts discriminators from a compiled rule by analyzing its conditions.
        /// </summary>
        private RuleDiscriminators ExtractDiscriminators(CompiledRule rule)
        {
            var discriminators = new RuleDiscriminators
            {
                Methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                PathPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            if (rule.OriginalRule == null)
                return discriminators;

            // Analyze groups (new schema)
            if (rule.OriginalRule.Groups != null)
            {
                foreach (var group in rule.OriginalRule.Groups)
                {
                    if (group?.Conditions == null) continue;

                    foreach (var condition in group.Conditions)
                    {
                        AnalyzeCondition(condition, discriminators);
                    }
                }
            }

            // Analyze flat conditions (legacy schema)
            if (rule.OriginalRule.Conditions != null)
            {
                foreach (var condition in rule.OriginalRule.Conditions)
                {
                    AnalyzeCondition(condition, discriminators);
                }
            }

            return discriminators;
        }

        /// <summary>
        /// Analyzes a single condition to extract discriminators.
        /// </summary>
        private static void AnalyzeCondition(WafCondition condition, RuleDiscriminators discriminators)
        {
            // HTTP Method (FieldId = 7)
            if (condition.FieldId == 7 && (condition.OperatorId == 1 || condition.OperatorId == 11 || condition.OperatorId == 13))
            {
                // equals, is in, is in list
                if (condition.OperatorId == 1) // equals
                {
                    discriminators.Methods.Add(condition.Valor);
                }
                else // is in list
                {
                    var methods = condition.Valor.Split(',').Select(m => m.Trim());
                    foreach (var method in methods)
                    {
                        discriminators.Methods.Add(method);
                    }
                }
            }

            // Path (FieldId = 13 or 14) with starts with (OperatorId = 7)
            if ((condition.FieldId == 13 || condition.FieldId == 14) && condition.OperatorId == 7)
            {
                // Path starts with - extract first segment
                var prefix = GetPathPrefix(condition.Valor);
                if (!string.IsNullOrEmpty(prefix))
                {
                    discriminators.PathPrefixes.Add(prefix);
                }
            }
        }

        /// <summary>
        /// Gets the first path segment (e.g., "/api/users" -> "/api").
        /// </summary>
        private static string GetPathPrefix(string path)
        {
            if (string.IsNullOrEmpty(path) || path[0] != '/')
                return string.Empty;

            var secondSlash = path.IndexOf('/', 1);
            if (secondSlash > 0)
            {
                return path.Substring(0, secondSlash).ToLower();
            }

            return path.ToLower();
        }

        /// <summary>
        /// Gets statistics about the index.
        /// </summary>
        public IndexStatistics GetStatistics()
        {
            return new IndexStatistics
            {
                TotalRules = _allRules.Count,
                MethodIndexedRules = _methodIndex.Values.SelectMany(r => r).Distinct().Count(),
                PathIndexedRules = _pathPrefixIndex.Values.SelectMany(r => r).Distinct().Count(),
                GenericRules = _genericRules.Count,
                MethodIndexSize = _methodIndex.Count,
                PathIndexSize = _pathPrefixIndex.Count
            };
        }
    }

    /// <summary>
    /// Represents discriminators extracted from a rule.
    /// </summary>
    internal class RuleDiscriminators
    {
        public HashSet<string> Methods { get; set; }
        public HashSet<string> PathPrefixes { get; set; }
    }

    /// <summary>
    /// Statistics about rule indexing.
    /// </summary>
    public class IndexStatistics
    {
        /// <summary>
        /// Total number of rules in the set.
        /// </summary>
        public int TotalRules { get; set; }

        /// <summary>
        /// Method-indexed rules.
        /// </summary>
        public int MethodIndexedRules { get; set; }

        /// <summary>
        /// Path-indexed rules.
        /// </summary>
        public int PathIndexedRules { get; set; }

        /// <summary>
        /// Generic rules without discriminators.
        /// </summary>
        public int GenericRules { get; set; }

        /// <summary>
        /// Size of the method index.
        /// </summary>
        public int MethodIndexSize { get; set; }

        /// <summary>
        /// Size of the path index.
        /// </summary>
        public int PathIndexSize { get; set; }

        /// <summary>
        /// Returns a string representation of the statistics.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
            => $"Total: {TotalRules}, Method-Indexed: {MethodIndexedRules}, Path-Indexed: {PathIndexedRules}, Generic: {GenericRules}";
    }
}