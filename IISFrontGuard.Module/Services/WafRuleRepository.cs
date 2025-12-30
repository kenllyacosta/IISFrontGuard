using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Caching;
using System;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Repository for accessing WAF rules and conditions from a SQL Server database with caching.
    /// </summary>
    public class WafRuleRepository : IWafRuleRepository
    {
        private readonly ICacheProvider _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="WafRuleRepository"/> class.
        /// </summary>
        /// <param name="cache">The cache provider for storing fetched rules.</param>
        public WafRuleRepository(ICacheProvider cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Fetches all enabled WAF rules for a specific host from the database, with 5-minute caching.
        /// </summary>
        /// <param name="host">The hostname to fetch rules for.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>An enumerable collection of WAF rules, or an empty collection if the host is invalid.</returns>
        public IEnumerable<WafRule> FetchWafRules(string host, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(host) || host.Length > 255)
            {
                return Enumerable.Empty<WafRule>();
            }

            host = new Uri($"https://{host}").Host.ToLowerInvariant();
            var cacheKey = $"WAF_RULES_{host}";

            if (_cache.Get(cacheKey) is List<WafRule> cachedRules)
            {
                return cachedRules;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"SELECT r.Id, r.Nombre, r.ActionId as ActionId, r.Prioridad, r.Habilitado, r.AppId FROM WafRuleEntity r INNER JOIN AppEntity a ON r.AppId = a.Id WHERE a.Host = @Host AND r.Habilitado = 1", connection)
                {
                    CommandTimeout = 5
                };
                command.Parameters.AddWithValue("@Host", host);

                var reader = command.ExecuteReader();
                var rules = new List<WafRule>();
                while (reader.Read())
                {
                    rules.Add(new WafRule
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        ActionId = reader.GetByte(2),
                        Prioridad = reader.GetInt32(3),
                        Habilitado = reader.GetBoolean(4),
                        AppId = reader.GetGuid(5),
                        Conditions = FetchWafConditions(reader.GetInt32(0), connectionString)
                    });
                }

                _cache.Insert(cacheKey, rules, null, DateTime.UtcNow.AddMinutes(1), Cache.NoSlidingExpiration);

                return rules;
            }
        }

        /// <summary>
        /// Fetches all conditions for a specific WAF rule from the database.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>A list of WAF conditions associated with the rule.</returns>
        public List<WafCondition> FetchWafConditions(int ruleId, string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"SELECT FieldId, OperatorId, Valor, LogicOperator, WafRuleEntityId, FieldName, ConditionOrder FROM WafConditionEntity WHERE WafRuleEntityId = @RuleId", connection);
                command.Parameters.AddWithValue("@RuleId", ruleId);

                var reader = command.ExecuteReader();
                var conditions = new List<WafCondition>();
                while (reader.Read())
                {
                    conditions.Add(new WafCondition
                    {
                        FieldId = reader.GetByte(0),
                        OperatorId = reader.GetByte(1),
                        Valor = reader.GetString(2).ToLower(),
                        LogicOperator = reader.GetByte(3),
                        WafRuleEntityId = reader.GetInt32(4),
                        FieldName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        ConditionOrder = reader.GetInt32(6)
                    });
                }
                return conditions;
            }
        }
    }
}
