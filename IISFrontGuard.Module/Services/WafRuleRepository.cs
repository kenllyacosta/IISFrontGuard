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
        private const string RuleIdParameter = "@RuleId";

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
        /// Supports both group-based (new) and flat condition (legacy) schemas.
        /// </summary>
        /// <param name="host">The hostname to fetch rules for.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>An enumerable collection of WAF rules, or an empty collection if the host is invalid.</returns>
        public IEnumerable<WafRule> FetchWafRules(string host, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(host) || host.Length > 255)
                return Enumerable.Empty<WafRule>();

            host = new Uri($"https://{host}").Host.ToLowerInvariant();
            var cacheKey = $"WAF_RULES_{host}";

            if (_cache.Get(cacheKey) is List<WafRule> cachedRules)
                return cachedRules;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                    SELECT r.Id, r.Nombre, r.ActionId as ActionId, r.Prioridad, r.Habilitado, r.AppId 
                    FROM WafRuleEntity r 
                    INNER JOIN AppEntity a ON r.AppId = a.Id 
                    WHERE a.Host = @Host AND r.Habilitado = 1", connection)
                {
                    CommandTimeout = 5
                };
                command.Parameters.AddWithValue("@Host", host);

                // Read all rule data first, then close the reader
                var rules = new List<WafRule>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rules.Add(new WafRule
                        {
                            Id = reader.GetInt32(0),
                            Nombre = reader.GetString(1),
                            ActionId = reader.GetByte(2),
                            Prioridad = reader.GetInt32(3),
                            Habilitado = reader.GetBoolean(4),
                            AppId = reader.GetGuid(5)
                        });
                    }
                }
                // Reader is now closed - safe to execute more queries

                // Load groups and conditions for each rule
                foreach (var rule in rules)
                    LoadRuleGroupsAndConditions(rule, connectionString, connection);

                _cache.Insert(cacheKey, rules, null, DateTime.UtcNow.AddMinutes(1), Cache.NoSlidingExpiration);

                return rules;
            }
        }

        /// <summary>
        /// Loads groups and conditions for a WAF rule with backward compatibility.
        /// Checks if the rule uses the new group-based schema or legacy flat schema.
        /// </summary>
        /// <param name="rule">The WAF rule to populate with groups and conditions.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="sharedConnection">Optional shared connection to reuse.</param>
        private void LoadRuleGroupsAndConditions(WafRule rule, string connectionString, SqlConnection sharedConnection = null)
        {
            bool closeConnection = false;
            SqlConnection connection = sharedConnection;

            if (connection == null)
            {
                connection = new SqlConnection(connectionString);
                connection.Open();
                closeConnection = true;
            }

            try
            {
                // First, check if this rule has groups (new schema)
                var groups = FetchWafGroups(rule.Id, connection);

                if (groups.Count > 0)
                {
                    // New group-based schema
                    rule.Groups = groups;
                    rule.Conditions = new List<WafCondition>(); // Empty for group-based rules
                }
                else
                {
                    // Legacy flat schema - load conditions directly
                    rule.Conditions = FetchWafConditionsLegacy(rule.Id, connection);
                    rule.Groups = new List<WafGroup>(); // Empty for legacy rules
                }
            }
            finally
            {
                if (closeConnection && connection != null)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        /// <summary>
        /// Fetches all groups for a specific WAF rule from the database (new schema).
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connection">The database connection.</param>
        /// <returns>A list of WAF groups with their conditions.</returns>
        private List<WafGroup> FetchWafGroups(int ruleId, SqlConnection connection)
        {
            var groups = new List<WafGroup>();

            var command = new SqlCommand(@"
                SELECT Id, GroupOrder 
                FROM WafGroups 
                WHERE WafRuleId = @RuleId 
                ORDER BY GroupOrder", connection)
            {
                CommandTimeout = 5
            };
            command.Parameters.AddWithValue(RuleIdParameter, ruleId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var groupId = reader.GetInt32(0);
                    groups.Add(new WafGroup
                    {
                        Id = groupId,
                        Conditions = new List<WafCondition>()
                    });
                }
            }

            // Load conditions for each group
            for (int i = 0; i < groups.Count; i++)
            {
                var groupCommand = new SqlCommand(@"
                    SELECT g.Id 
                    FROM WafGroups g 
                    WHERE g.WafRuleId = @RuleId 
                    ORDER BY g.GroupOrder 
                    OFFSET @Offset ROWS 
                    FETCH NEXT 1 ROWS ONLY", connection)
                {
                    CommandTimeout = 5
                };
                groupCommand.Parameters.AddWithValue(RuleIdParameter, ruleId);
                groupCommand.Parameters.AddWithValue("@Offset", i);

                var groupId = (int)groupCommand.ExecuteScalar();
                groups[i].Conditions = FetchGroupConditions(groupId, connection);
            }

            return groups;
        }

        /// <summary>
        /// Fetches all conditions for a specific WAF group from the database (new schema).
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="connection">The database connection.</param>
        /// <returns>A list of WAF conditions in the group.</returns>
        private List<WafCondition> FetchGroupConditions(int groupId, SqlConnection connection)
        {
            var conditions = new List<WafCondition>();

            var command = new SqlCommand(@"
                SELECT Id, FieldId, OperatorId, Valor, FieldName, WafGroupId, WafRuleEntityId, CreationDate, 
                       ISNULL(Negate, 0) as Negate
                FROM WafConditionEntity 
                WHERE WafGroupId = @GroupId 
                ORDER BY Id", connection)
            {
                CommandTimeout = 5
            };
            command.Parameters.AddWithValue("@GroupId", groupId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    conditions.Add(ReadConditionFromReader(reader));
                }
            }

            return conditions;
        }

        /// <summary>
        /// Reads a WafCondition from a SqlDataReader (new schema format with 9 columns).
        /// </summary>
        /// <param name="reader">The data reader positioned at a condition row.</param>
        /// <returns>A WafCondition populated from the current reader row.</returns>
        private static WafCondition ReadConditionFromReader(SqlDataReader reader)
        {
            return new WafCondition
            {
                Id = reader.GetInt32(0),
                FieldId = reader.GetByte(1),
                OperatorId = reader.GetByte(2),
                Valor = reader.IsDBNull(3) ? string.Empty : reader.GetString(3).ToLower(),
                FieldName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                WafGroupId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
#pragma warning disable CS0618 // Type or member is obsolete
                WafRuleEntityId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
#pragma warning restore CS0618 // Type or member is obsolete
                CreationDate = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                Negate = reader.GetBoolean(8)
            };
        }

        /// <summary>
        /// Fetches all conditions for a specific WAF rule using the legacy flat schema.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connection">The database connection.</param>
        /// <returns>A list of WAF conditions.</returns>
        private List<WafCondition> FetchWafConditionsLegacy(int ruleId, SqlConnection connection)
        {
            var conditions = new List<WafCondition>();

            var command = new SqlCommand(@"
                SELECT Id, FieldId, OperatorId, Valor, LogicOperator, WafRuleEntityId, FieldName, ConditionOrder, CreationDate 
                FROM WafConditionEntity 
                WHERE WafRuleEntityId = @RuleId AND (WafGroupId IS NULL OR WafGroupId = 0)
                ORDER BY ConditionOrder", connection)
            {
                CommandTimeout = 5
            };
            command.Parameters.AddWithValue(RuleIdParameter, ruleId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    conditions.Add(new WafCondition
                    {
                        Id = reader.GetInt32(0),
                        FieldId = reader.GetByte(1),
                        OperatorId = reader.GetByte(2),
                        Valor = reader.IsDBNull(3) ? string.Empty : reader.GetString(3).ToLower(),
#pragma warning disable CS0618 // Type or member is obsolete
                        LogicOperator = reader.IsDBNull(4) ? (byte?)null : reader.GetByte(4),
                        WafRuleEntityId = reader.GetInt32(5),
#pragma warning restore CS0618 // Type or member is obsolete
                        FieldName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
#pragma warning disable CS0618 // Type or member is obsolete
                        ConditionOrder = reader.GetInt32(7),
#pragma warning restore CS0618 // Type or member is obsolete
                        CreationDate = reader.IsDBNull(8) ? DateTime.UtcNow : reader.GetDateTime(8),
                        WafGroupId = null, // Legacy conditions don't have groups
                        Negate = false
                    });
                }
            }

            return conditions;
        }

        /// <summary>
        /// Fetches all conditions for a specific WAF rule from the database.
        /// This method is kept for backward compatibility and interface compliance.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>A list of WAF conditions associated with the rule.</returns>
        public List<WafCondition> FetchWafConditions(int ruleId, string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Check if rule uses groups (new schema)
                var groupCount = GetGroupCount(ruleId, connection);

                if (groupCount > 0)
                {
                    // New schema: return all conditions from all groups flattened
                    return FetchAllConditionsFromGroups(ruleId, connection);
                }
                else
                {
                    // Legacy schema: return flat conditions
                    return FetchWafConditionsLegacy(ruleId, connection);
                }
            }
        }

        /// <summary>
        /// Gets the number of groups associated with a rule.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connection">The database connection.</param>
        /// <returns>The number of groups for this rule.</returns>
        private int GetGroupCount(int ruleId, SqlConnection connection)
        {
            var command = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM WafGroups 
                WHERE WafRuleId = @RuleId", connection)
            {
                CommandTimeout = 5
            };
            command.Parameters.AddWithValue(RuleIdParameter, ruleId);
            return (int)command.ExecuteScalar();
        }

        /// <summary>
        /// Fetches all conditions from all groups for a rule (flattened list).
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connection">The database connection.</param>
        /// <returns>A flattened list of all conditions across all groups.</returns>
        private List<WafCondition> FetchAllConditionsFromGroups(int ruleId, SqlConnection connection)
        {
            var conditions = new List<WafCondition>();

            var command = new SqlCommand(@"
                SELECT c.Id, c.FieldId, c.OperatorId, c.Valor, c.FieldName, c.WafGroupId, c.WafRuleEntityId, c.CreationDate,
                       ISNULL(c.Negate, 0) as Negate
                FROM WafConditionEntity c
                INNER JOIN WafGroups g ON c.WafGroupId = g.Id
                WHERE g.WafRuleId = @RuleId
                ORDER BY g.GroupOrder, c.Id", connection)
            {
                CommandTimeout = 5
            };
            command.Parameters.AddWithValue(RuleIdParameter, ruleId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    conditions.Add(ReadConditionFromReader(reader));
                }
            }

            return conditions;
        }
    }
}
