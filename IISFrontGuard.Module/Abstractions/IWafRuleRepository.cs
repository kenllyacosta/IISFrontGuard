using IISFrontGuard.Module.Models;
using System.Collections.Generic;

namespace IISFrontGuard.Module.Abstractions
{
    /// <summary>
    /// Defines an abstraction for accessing WAF rules and conditions from a data store.
    /// </summary>
    public interface IWafRuleRepository
    {
        /// <summary>
        /// Fetches all WAF rules for a specific host.
        /// </summary>
        /// <param name="host">The hostname to fetch rules for.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>An enumerable collection of WAF rules.</returns>
        IEnumerable<WafRule> FetchWafRules(string host, string connectionString);

        /// <summary>
        /// Fetches all conditions for a specific WAF rule.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>A list of WAF conditions.</returns>
        List<WafCondition> FetchWafConditions(int ruleId, string connectionString);
    }
}
