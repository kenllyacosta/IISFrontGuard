using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Compiles WAF rules into optimized delegates for fast evaluation.
    /// Conditions are pre-compiled to avoid interpretive evaluation on every request.
    /// </summary>
    public class RuleCompiler
    {
        /// <summary>
        /// Compiles a WAF rule into an optimized evaluation delegate.
        /// The resulting CompiledRule can be evaluated much faster than the original.
        /// </summary>
        public CompiledRule CompileRule(WafRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            var compiled = new CompiledRule
            {
                Id = rule.Id,
                Name = rule.Nombre,
                ActionId = rule.ActionId,
                Priority = rule.Prioridad ?? 0,
                AppId = rule.AppId,
                OriginalRule = rule
            };

            // Compile based on schema type
            if (rule.Groups != null && rule.Groups.Count > 0)
            {
                // New group-based schema: OR across groups
                compiled.Evaluate = CompileGroupBasedRule(rule.Groups);
            }
            else if (rule.Conditions != null && rule.Conditions.Count > 0)
            {
                // Legacy flat schema
                compiled.Evaluate = CompileLegacyRule(rule.Conditions);
            }
            else
            {
                // No conditions - always false
                compiled.Evaluate = ctx => false;
            }

            return compiled;
        }

        /// <summary>
        /// Compiles a group-based rule: (Group1) OR (Group2) OR (Group3)
        /// </summary>
        private static Func<RequestContext, bool> CompileGroupBasedRule(List<WafGroup> groups)
        {
            var compiledGroups = groups
                .Select(CompileGroup)
                .ToArray();

            // Return OR delegate - short-circuits on first match
            return ctx => compiledGroups.Any(groupEval => groupEval(ctx));
        }

        /// <summary>
        /// Compiles a single group: Condition1 AND Condition2 AND Condition3
        /// </summary>
        private static Func<RequestContext, bool> CompileGroup(WafGroup group)
        {
            if (group?.Conditions == null || group.Conditions.Count == 0)
                return ctx => false;

            var compiledConditions = group.Conditions
                .Select(CompileCondition)
                .ToArray();

            // Return AND delegate with fail-fast
            return ctx => compiledConditions.All(conditionEval => conditionEval(ctx));
        }

        /// <summary>
        /// Compiles a legacy flat rule with LogicOperator.
        /// </summary>
        private Func<RequestContext, bool> CompileLegacyRule(List<WafCondition> conditions)
        {
            var compiledConditions = conditions
                .Select(c => new
                {
                    Evaluate = CompileCondition(c),
                    LogicOp = c.LogicOperator ?? 1
                })
                .ToArray();

            return ctx =>
            {
                bool result = true;
                foreach (var compiled in compiledConditions)
                {
                    bool match = compiled.Evaluate(ctx);

                    if (compiled.LogicOp == 1 && !match) // AND
                        return false;

                    if (compiled.LogicOp == 2 && match) // OR
                        return true;
                }
                return result;
            };
        }

        /// <summary>
        /// Compiles a single condition into an optimized evaluation delegate.
        /// This is where the magic happens - we pre-build the comparison logic.
        /// </summary>
        private static Func<RequestContext, bool> CompileCondition(WafCondition condition)
        {
            // Get the field extractor
            var fieldExtractor = GetFieldExtractor(condition.FieldId, condition.FieldName);

            // Get the operator evaluator
            var operatorEval = GetOperatorEvaluator(condition.OperatorId, condition.Valor);

            // Combine field extraction + operator + negation
            var negate = condition.Negate;

            return ctx =>
            {
                var fieldValue = fieldExtractor(ctx).ToLower();
                var match = operatorEval(fieldValue);
                return negate ? !match : match;
            };
        }

        /// <summary>
        /// Returns a delegate that extracts the specified field from a RequestContext.
        /// Field extraction is optimized to use pre-cached values.
        /// </summary>
        private static Func<RequestContext, string> GetFieldExtractor(byte fieldId, string fieldName)
        {
            switch (fieldId)
            {
                case 1: // cookie
                    return ctx => ctx.GetCookie(fieldName);
                case 2: // hostname
                    return ctx => ctx.Host;
                case 3: // ip
                case 4: // ip-range
                    return ctx => ctx.ClientIp;
                case 5: // protocol
                    return ctx => ctx.Protocol;
                case 6: // referrer
                    return ctx => ctx.Referrer;
                case 7: // method
                    return ctx => ctx.Method;
                case 8: // httpversion
                    return ctx => ctx.HttpVersion;
                case 9: // user-agent
                    return ctx => ctx.UserAgent;
                case 10: // x-forwarded-for
                    return ctx => ctx.XForwardedFor;
                case 11: // mimetype
                    return ctx => ctx.MimeType;
                case 12: // url-full
                    return ctx => ctx.FullUrl;
                case 13: // url (absolute path)
                    return ctx => ctx.Path;
                case 14: // url-path-and-query
                    return ctx => ctx.PathAndQuery;
                case 15: // url-querystring
                    return ctx => ctx.QueryString;
                case 16: // header
                    return ctx => ctx.GetHeader(fieldName);
                case 17: // content-type
                    return ctx => ctx.ContentType;
                case 18: // body
                    return ctx => ctx.GetBody();
                case 19: // body length
                    return ctx => ctx.BodyLength.ToString();
                case 20: // country
                    return ctx => ctx.CountryName;
                case 21: // country-iso2
                    return ctx => ctx.CountryIso2;
                case 22: // continent
                    return ctx => ctx.ContinentName;
                case 23: // CF-Connecting-IP
                    return ctx => ctx.GetClientIpFromHeader("CF-Connecting-IP");
                case 24: // X-Forwarded-For (from header)
                    return ctx => ctx.GetClientIpFromHeader("X-Forwarded-For");
                case 25: // True-Client-IP
                    return ctx => ctx.GetClientIpFromHeader("True-Client-IP");
                default:
                    return ctx => string.Empty;
            }
        }

        /// <summary>
        /// Returns a delegate that performs the specified comparison operation.
        /// Operators are pre-compiled to avoid switch statements at runtime.
        /// </summary>
        private static Func<string, bool> GetOperatorEvaluator(byte operatorId, string targetValue)
        {
            // Normalize target value once during compilation
            var normalizedTarget = targetValue?.ToLower() ?? string.Empty;

            switch (operatorId)
            {
                case 1: // equals
                    return fieldValue => fieldValue == normalizedTarget;
                case 2: // not equals
                    return fieldValue => fieldValue != normalizedTarget;
                case 3: // contains
                    return fieldValue => fieldValue.Contains(normalizedTarget);
                case 4: // not contains
                    return fieldValue => !fieldValue.Contains(normalizedTarget);
                case 5: // matches regex
                    {
                        var regex = new Regex(normalizedTarget, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
                        return fieldValue => regex.IsMatch(fieldValue);
                    }

                case 6: // not matches regex
                    {
                        var regex = new Regex(normalizedTarget, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
                        return fieldValue => !regex.IsMatch(fieldValue);
                    }

                case 7: // starts with
                    return fieldValue => fieldValue.StartsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase);
                case 8: // not starts with
                    return fieldValue => !fieldValue.StartsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase);
                case 9: // ends with
                    return fieldValue => fieldValue.EndsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase);
                case 10: // not ends with
                    return fieldValue => !fieldValue.EndsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase);
                case 11: // is in
                case 13: // is in list
                    {
                        var values = normalizedTarget.Split(',').Select(v => v.Trim()).ToArray();
                        return fieldValue => values.Contains(fieldValue);
                    }

                case 12: // is not in
                case 14: // is not in list
                    {
                        var values = normalizedTarget.Split(',').Select(v => v.Trim()).ToArray();
                        return fieldValue => !values.Contains(fieldValue);
                    }

                case 15: // is ip in range
                    {
                        var ipValidator = new IpValidator(normalizedTarget.Split(','));
                        return fieldValue => ipValidator.IsInIp(fieldValue);
                    }

                case 16: // is ip not in range
                    {
                        var ipValidator = new IpValidator(normalizedTarget.Split(','));
                        return fieldValue => !ipValidator.IsInIp(fieldValue);
                    }

                case 17: // greater than
                    {
                        if (long.TryParse(normalizedTarget, out var targetLong))
                            return fieldValue => long.TryParse(fieldValue, out var val) && val > targetLong;
                        return fieldValue => false;
                    }

                case 18: // less than
                    {
                        if (long.TryParse(normalizedTarget, out var targetLong))
                            return fieldValue => long.TryParse(fieldValue, out var val) && val < targetLong;
                        return fieldValue => false;
                    }

                case 19: // greater than or equal to
                    {
                        if (long.TryParse(normalizedTarget, out var targetLong))
                            return fieldValue => long.TryParse(fieldValue, out var val) && val >= targetLong;
                        return fieldValue => false;
                    }

                case 20: // less than or equal to
                    {
                        if (long.TryParse(normalizedTarget, out var targetLong))
                            return fieldValue => long.TryParse(fieldValue, out var val) && val <= targetLong;
                        return fieldValue => false;
                    }

                case 21: // is present
                    return fieldValue => !string.IsNullOrEmpty(fieldValue);
                case 22: // is not present
                    return fieldValue => string.IsNullOrEmpty(fieldValue);
                default:
                    return fieldValue => false;
            }
        }
    }
}