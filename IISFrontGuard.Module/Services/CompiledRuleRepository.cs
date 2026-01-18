using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Caching wrapper around WafRuleRepository that pre-compiles rules for fast evaluation.
    /// Compiled rules are cached in memory and reused across requests.
    /// Uses indexing to quickly filter candidate rules based on discriminators.
    /// </summary>
    public class CompiledRuleRepository
    {
        private readonly IWafRuleRepository _ruleRepository;
        private readonly RuleCompiler _compiler;
        private readonly ICacheProvider _cache;
        private const int CacheExpirationMinutes = 5;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompiledRuleRepository"/> class.
        /// </summary>
        /// <param name="ruleRepository"></param>
        /// <param name="cache"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public CompiledRuleRepository(IWafRuleRepository ruleRepository, ICacheProvider cache)
        {
            _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _compiler = new RuleCompiler();
        }

        /// <summary>
        /// Fetches and compiles WAF rules for a host, with indexing for fast candidate filtering.
        /// Compiled rule sets are cached for fast repeated access.
        /// </summary>
        public IndexedCompiledRuleSet GetIndexedCompiledRules(string host, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(host))
                return new IndexedCompiledRuleSet(new List<CompiledRule>());

            var cacheKey = $"INDEXED_COMPILED_WAF_RULES_{host}";

            // Try cache first
            if (_cache.Get(cacheKey) is IndexedCompiledRuleSet cachedRuleSet)
                return cachedRuleSet;

            // Fetch and compile rules
            var compiledRules = GetCompiledRules(host, connectionString);

            // Build indexed rule set
            var indexedRuleSet = new IndexedCompiledRuleSet(compiledRules);

            // Cache indexed rule set
            var absoluteExpiration = DateTime.UtcNow.AddMinutes(CacheExpirationMinutes).ToLocalTime();
            _cache.Insert(cacheKey, indexedRuleSet, null, absoluteExpiration, System.Web.Caching.Cache.NoSlidingExpiration);

            return indexedRuleSet;
        }

        /// <summary>
        /// Fetches and compiles WAF rules for a host.
        /// Compiled rules are cached for fast repeated access.
        /// </summary>
        public List<CompiledRule> GetCompiledRules(string host, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(host))
                return new List<CompiledRule>();

            var cacheKey = $"COMPILED_WAF_RULES_{host}";

            // Try cache first
            if (_cache.Get(cacheKey) is List<CompiledRule> cachedRules)
                return cachedRules;

            // Fetch from repository
            var rules = _ruleRepository.FetchWafRules(host, connectionString);

            // Compile rules
            var compiledRules = rules
                .Where(r => r.Habilitado) // Only compile enabled rules
                .Select(r =>
                {
                    try
                    {
                        return _compiler.CompileRule(r);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError($"Failed to compile rule {r.Id} ({r.Nombre}): {ex.Message}");
                        // Return fallback uncompiled rule
                        return new CompiledRule
                        {
                            Id = r.Id,
                            Name = r.Nombre,
                            ActionId = r.ActionId,
                            Priority = r.Prioridad ?? 0,
                            AppId = r.AppId,
                            OriginalRule = r,
                            Evaluate = ctx => false // Safe fallback
                        };
                    }
                })
                .OrderBy(r => r.Priority)
                .ToList();

            // Cache compiled rules
            var absoluteExpiration = DateTime.UtcNow.AddMinutes(CacheExpirationMinutes).ToLocalTime();
            _cache.Insert(cacheKey, compiledRules, null, absoluteExpiration, System.Web.Caching.Cache.NoSlidingExpiration);

            return compiledRules;
        }

        /// <summary>
        /// Invalidates the compiled rule cache for a specific host.
        /// Call this when rules are modified.
        /// </summary>
        public void InvalidateCache(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return;

            _cache.Remove($"COMPILED_WAF_RULES_{host}");
            _cache.Remove($"INDEXED_COMPILED_WAF_RULES_{host}");
        }

        /// <summary>
        /// Invalidates all compiled rule caches.
        /// </summary>
        public void InvalidateAllCaches()
        {
            // Note: This is a simplistic implementation
            // In production, you might want to track all cache keys
            // For now, rely on natural cache expiration
        }
    }
}

