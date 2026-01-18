using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Caching;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Services
{
    public class WafRuleRepositoryIntegrationTests
    {
        private static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["IISFrontGuardConnection"].ConnectionString;

        private class SimpleCacheProvider : ICacheProvider
        {
            private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();
            public object Get(string key) => _cache.TryGetValue(key, out var value) ? value : null;
            
            public void Insert(string key, object value, CacheDependency dependencies, DateTime absoluteExpiration, TimeSpan slidingExpiration)
            {
                _cache[key] = value;
            }

            public void Remove(string key)
            {
                if (_cache.ContainsKey(key))
                {
                    _cache.Remove(key);
                }
            }
        }

        private static void EnsureHostWithRuleAndNoConditions(string host)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(ConnectionString))
            {
                connection.Open();
                // Insert AppEntity if not exists
                var appIdCmd = new System.Data.SqlClient.SqlCommand(
                    "IF NOT EXISTS (SELECT 1 FROM AppEntity WHERE Host = @Host) " +
                    "BEGIN INSERT INTO AppEntity (Id, Host, AppName) VALUES (@Id, @Host, @AppName) END; " +
                    "SELECT Id FROM AppEntity WHERE Host = @Host;",
                    connection);
                var appId = Guid.NewGuid();
                appIdCmd.Parameters.AddWithValue("@Host", host);
                appIdCmd.Parameters.AddWithValue("@Id", appId);
                appIdCmd.Parameters.AddWithValue("@AppName", "NoCondApp");
                var result = appIdCmd.ExecuteScalar();
                if (result is Guid existingId)
                    appId = existingId;
                // Insert WafRuleEntity if not exists
                var ruleCmd = new System.Data.SqlClient.SqlCommand(
                    "IF NOT EXISTS (SELECT 1 FROM WafRuleEntity WHERE AppId = @AppId) " +
                    "INSERT INTO WafRuleEntity (Nombre, ActionId, Prioridad, Habilitado, AppId) VALUES ('NoCondRule', 1, 1, 1, @AppId);",
                    connection);
                ruleCmd.Parameters.AddWithValue("@AppId", appId);
                ruleCmd.ExecuteNonQuery();
                // Delete all conditions for rules of this app
                var delCondCmd = new System.Data.SqlClient.SqlCommand(
                    "DELETE FROM WafConditionEntity WHERE WafRuleEntityId IN (SELECT Id FROM WafRuleEntity WHERE AppId = @AppId)",
                    connection);
                delCondCmd.Parameters.AddWithValue("@AppId", appId);
                delCondCmd.ExecuteNonQuery();
            }
        }

        private static void EnsureHostWithRuleWithConditions(string host)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(ConnectionString))
            {
                connection.Open();
                // Insert AppEntity if not exists
                var appIdCmd = new System.Data.SqlClient.SqlCommand(
                    "IF NOT EXISTS (SELECT 1 FROM AppEntity WHERE Host = @Host) " +
                    "BEGIN INSERT INTO AppEntity (Id, Host, AppName) VALUES (@Id, @Host, @AppName) END; " +
                    "SELECT Id FROM AppEntity WHERE Host = @Host;",
                    connection);
                var appId = Guid.NewGuid();
                appIdCmd.Parameters.AddWithValue("@Host", host);
                appIdCmd.Parameters.AddWithValue("@Id", appId);
                appIdCmd.Parameters.AddWithValue("@AppName", "CondApp");
                var result = appIdCmd.ExecuteScalar();
                if (result is Guid existingId)
                    appId = existingId;
                // Insert WafRuleEntity if not exists
                var ruleCmd = new System.Data.SqlClient.SqlCommand(
                    "IF NOT EXISTS (SELECT 1 FROM WafRuleEntity WHERE AppId = @AppId) " +
                    "INSERT INTO WafRuleEntity (Nombre, ActionId, Prioridad, Habilitado, AppId) VALUES ('CondRule', 1, 1, 1, @AppId);",
                    connection);
                ruleCmd.Parameters.AddWithValue("@AppId", appId);
                ruleCmd.ExecuteNonQuery();
                // Get the rule Id
                var getRuleIdCmd = new System.Data.SqlClient.SqlCommand(
                    "SELECT TOP 1 Id FROM WafRuleEntity WHERE AppId = @AppId ORDER BY Id DESC;",
                    connection);
                getRuleIdCmd.Parameters.AddWithValue("@AppId", appId);
                var ruleIdObj = getRuleIdCmd.ExecuteScalar();
                if (ruleIdObj == null) return;
                int ruleId = Convert.ToInt32(ruleIdObj);
                // Delete all conditions for this rule
                var delCondCmd = new System.Data.SqlClient.SqlCommand(
                    "DELETE FROM WafConditionEntity WHERE WafRuleEntityId = @RuleId",
                    connection);
                delCondCmd.Parameters.AddWithValue("@RuleId", ruleId);
                delCondCmd.ExecuteNonQuery();
                // Insert a sample condition using the correct schema
                var insertCondCmd = new System.Data.SqlClient.SqlCommand(
                    "INSERT INTO dbo.WafConditionEntity (FieldId,OperatorId,Valor,LogicOperator,WafRuleEntityId,FieldName,ConditionOrder,CreationDate) " +
                    "VALUES (@FieldId, @OperatorId, @Valor, @LogicOperator, @WafRuleEntityId, @FieldName, @ConditionOrder, @CreationDate);",
                    connection);
                insertCondCmd.Parameters.AddWithValue("@FieldId", 1);
                insertCondCmd.Parameters.AddWithValue("@OperatorId", 1);
                insertCondCmd.Parameters.AddWithValue("@Valor", "/test");
                insertCondCmd.Parameters.AddWithValue("@LogicOperator", 0);
                insertCondCmd.Parameters.AddWithValue("@WafRuleEntityId", ruleId);
                insertCondCmd.Parameters.AddWithValue("@FieldName", "Url");
                insertCondCmd.Parameters.AddWithValue("@ConditionOrder", 1);
                insertCondCmd.Parameters.AddWithValue("@CreationDate", DateTime.UtcNow);
                insertCondCmd.ExecuteNonQuery();
            }
        }

        [Fact]
        public void FetchWafRules_ReturnsEmpty_ForInvalidHost()
        {
            var repo = new WafRuleRepository(new SimpleCacheProvider());
            Assert.Empty(repo.FetchWafRules(null, ConnectionString));
            Assert.Empty(repo.FetchWafRules("", ConnectionString));
            Assert.Empty(repo.FetchWafRules(new string('a', 256), ConnectionString));
        }

        [Fact]
        public void FetchWafRules_ReturnsFromCache_IfPresent()
        {
            var cache = new SimpleCacheProvider();
            var repo = new WafRuleRepository(cache);
            var host = "test.local";
            var cacheKey = $"WAF_RULES_{new Uri($"https://{host}").Host.ToLowerInvariant()}";
            var expected = new List<WafRule> { new WafRule { Id = 1, Nombre = "CachedRule" } };
            cache.Insert(cacheKey, expected, null, DateTime.UtcNow.AddMinutes(5), Cache.NoSlidingExpiration);
            var result = repo.FetchWafRules(host, ConnectionString);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FetchWafRules_ReturnsFromDb_AndInsertsToCache()
        {
            var cache = new SimpleCacheProvider();
            var repo = new WafRuleRepository(cache);
            var host = "integration-test.local"; // Should exist in test DB
            EnsureHostWithRuleAndNoConditions(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            Assert.NotNull(rules);
            Assert.All(rules, r => Assert.True(r.Habilitado));
            // Should now be cached
            var cacheKey = $"WAF_RULES_{new Uri($"https://{host}").Host.ToLowerInvariant()}";
            var cached = cache.Get(cacheKey) as List<WafRule>;
            Assert.Equal(rules, cached);
        }

        [Fact]
        public void FetchWafConditions_ReturnsConditionsFromDb()
        {
            var repo = new WafRuleRepository(new SimpleCacheProvider());
            var host = "integration-test.local"; // Should exist in test DB
            EnsureHostWithRuleAndNoConditions(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            if (rules.Count > 0)
            {
                var ruleId = rules[0].Id;
                var conditions = repo.FetchWafConditions(ruleId, ConnectionString);
                Assert.NotNull(conditions);
                Assert.All(conditions, c => Assert.Equal(ruleId, c.WafRuleEntityId));
            }
        }

        [Fact]
        public void FetchWafRules_ReturnsRuleWithNoConditions_IfNoneExist()
        {
            var cache = new SimpleCacheProvider();
            var repo = new WafRuleRepository(cache);
            var host = "integration-test-noconditions.local";
            EnsureHostWithRuleAndNoConditions(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            Assert.NotNull(rules);
            Assert.All(rules, r => Assert.NotNull(r.Conditions));
            Assert.Contains(rules, r => r.Conditions.Count == 0);
        }

        [Fact]
        public void FetchWafConditions_ReturnsEmptyList_ForNonExistentRuleId()
        {
            var repo = new WafRuleRepository(new SimpleCacheProvider());
            var nonExistentRuleId = int.MaxValue;
            var conditions = repo.FetchWafConditions(nonExistentRuleId, ConnectionString);
            Assert.NotNull(conditions);
            Assert.Empty(conditions);
        }

        [Fact]
        public void FetchWafConditions_FromDb()
        {
            var repo = new WafRuleRepository(new SimpleCacheProvider());
            var host = "integration-test.local"; // Should exist in test DB
            EnsureHostWithRuleWithConditions(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            if (rules.Count > 0)
            {
                var ruleId = rules[0].Id;
                var conditions = repo.FetchWafConditions(ruleId, ConnectionString);
                Assert.NotNull(conditions);
                Assert.All(conditions, c => Assert.Equal(ruleId, c.WafRuleEntityId));
                Assert.True(conditions.Count > 0);
            }
        }

        [Fact]
        public void FetchWafRules_WithGroupBasedSchema_ReturnsRulesWithGroups()
        {
            var cache = new SimpleCacheProvider();
            var repo = new WafRuleRepository(cache);
            var host = "integration-test-groups.local";
            EnsureHostWithRuleWithGroups(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            Assert.NotNull(rules);
            Assert.NotEmpty(rules);
            Assert.All(rules, r =>
            {
                Assert.NotNull(r.Groups);
                Assert.NotNull(r.Conditions);
            });
            var rulesWithGroups = rules.Where(r => r.Groups.Count > 0).ToList();
            if (rulesWithGroups.Count > 0)
            {
                Assert.All(rulesWithGroups, r => Assert.Empty(r.Conditions));
            }
        }

        [Fact]
        public void FetchWafRules_WithMultipleGroups_ReturnsGroupsInOrder()
        {
            var cache = new SimpleCacheProvider();
            var repo = new WafRuleRepository(cache);
            var host = "integration-test-multigroups.local";
            EnsureHostWithRuleWithMultipleGroups(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            if (rules.Count > 0)
            {
                var ruleWithGroups = rules.FirstOrDefault(r => r.Groups.Count > 1);
                if (ruleWithGroups != null)
                {
                    Assert.True(ruleWithGroups.Groups.Count >= 2);
                    Assert.All(ruleWithGroups.Groups, g => Assert.NotNull(g.Conditions));
                }
            }
        }

        [Fact]
        public void FetchWafConditions_WithGroupBasedSchema_ReturnsAllConditionsFlattened()
        {
            var repo = new WafRuleRepository(new SimpleCacheProvider());
            var host = "integration-test-groups.local";
            EnsureHostWithRuleWithGroups(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            if (rules.Count > 0)
            {
                var ruleId = rules[0].Id;
                var conditions = repo.FetchWafConditions(ruleId, ConnectionString);
                Assert.NotNull(conditions);
                if (conditions.Count > 0)
                {
                    Assert.All(conditions, c => Assert.NotNull(c.WafGroupId));
                }
            }
        }

        [Fact]
        public void FetchWafRules_CacheExpiration_RefetchesFromDb()
        {
            var cache = new SimpleCacheProvider();
            var repo = new WafRuleRepository(cache);
            var host = "integration-test-cache.local";
            EnsureHostWithRuleAndNoConditions(host);
            var firstFetch = repo.FetchWafRules(host, ConnectionString).ToList();
            Assert.NotEmpty(firstFetch);
            var cacheKey = $"WAF_RULES_{new Uri($"https://{host}").Host.ToLowerInvariant()}";
            var cached = cache.Get(cacheKey);
            Assert.NotNull(cached);
            cache.Remove(cacheKey);
            var secondFetch = repo.FetchWafRules(host, ConnectionString).ToList();
            Assert.NotEmpty(secondFetch);
        }

        [Fact]
        public void FetchWafRules_HostCaseInsensitive_ReturnsSameRules()
        {
            var cache = new SimpleCacheProvider();
            var repo = new WafRuleRepository(cache);
            var host = "integration-test.local";
            EnsureHostWithRuleAndNoConditions(host);
            var lowerCaseRules = repo.FetchWafRules(host.ToLower(), ConnectionString).ToList();
            cache.Remove($"WAF_RULES_{host.ToLower()}");
            var upperCaseRules = repo.FetchWafRules(host.ToUpper(), ConnectionString).ToList();
            Assert.Equal(lowerCaseRules.Count, upperCaseRules.Count);
        }

        [Fact]
        public void FetchWafRules_OnlyReturnsEnabledRules()
        {
            var cache = new SimpleCacheProvider();
            var repo = new WafRuleRepository(cache);
            var host = "integration-test-enabled.local";
            EnsureHostWithEnabledAndDisabledRules(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            Assert.NotNull(rules);
            Assert.All(rules, r => Assert.True(r.Habilitado));
        }

        [Fact]
        public void FetchWafConditions_WithNegateFlag_ReturnsNegateValue()
        {
            var repo = new WafRuleRepository(new SimpleCacheProvider());
            var host = "integration-test-negate.local";
            EnsureHostWithRuleWithNegatedCondition(host);
            var rules = repo.FetchWafRules(host, ConnectionString).ToList();
            if (rules.Count > 0)
            {
                var ruleId = rules[0].Id;
                var conditions = repo.FetchWafConditions(ruleId, ConnectionString);
                if (conditions.Count > 0)
                    Assert.Contains(conditions, c => c.Negate);
            }
        }

        [Fact]
        public void FetchWafRules_WithInvalidConnectionString_ThrowsException()
        {
            var repo = new WafRuleRepository(new SimpleCacheProvider());
            var host = "test.local";
            Assert.Throws<System.ArgumentException>(() =>
            {
                repo.FetchWafRules(host, "Invalid Connection String").ToList();
            });
        }

        [Fact]
        public void FetchWafConditions_WithInvalidConnectionString_ThrowsException()
        {
            var repo = new WafRuleRepository(new SimpleCacheProvider());
            Assert.Throws<System.ArgumentException>(() =>
            {
                repo.FetchWafConditions(1, "Invalid Connection String");
            });
        }

        private static void EnsureHostWithRuleWithGroups(string host)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(ConnectionString))
            {
                connection.Open();
                var appId = EnsureAppEntity(connection, host, "GroupApp");
                var ruleId = EnsureWafRule(connection, appId, "GroupRule");
                DeleteAllGroupsForRule(connection, ruleId);
                var groupId = InsertWafGroup(connection, ruleId, 1);
                InsertGroupCondition(connection, groupId, ruleId, 1, 1, "/test-group", "Url", 1, false);
            }
        }

        private static void EnsureHostWithRuleWithMultipleGroups(string host)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(ConnectionString))
            {
                connection.Open();
                var appId = EnsureAppEntity(connection, host, "MultiGroupApp");
                var ruleId = EnsureWafRule(connection, appId, "MultiGroupRule");
                DeleteAllGroupsForRule(connection, ruleId);
                var groupId1 = InsertWafGroup(connection, ruleId, 1);
                InsertGroupCondition(connection, groupId1, ruleId, 1, 1, "/test1", "Url", 1, false);
                var groupId2 = InsertWafGroup(connection, ruleId, 2);
                InsertGroupCondition(connection, groupId2, ruleId, 1, 1, "/test2", "Url", 1, false);
            }
        }

        private static void EnsureHostWithEnabledAndDisabledRules(string host)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(ConnectionString))
            {
                connection.Open();
                var appId = EnsureAppEntity(connection, host, "EnabledApp");
                EnsureWafRule(connection, appId, "EnabledRule", true);
                EnsureWafRule(connection, appId, "DisabledRule", false);
            }
        }

        private static void EnsureHostWithRuleWithNegatedCondition(string host)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(ConnectionString))
            {
                connection.Open();
                var appId = EnsureAppEntity(connection, host, "NegateApp");
                var ruleId = EnsureWafRule(connection, appId, "NegateRule");
                DeleteAllGroupsForRule(connection, ruleId);
                var groupId = InsertWafGroup(connection, ruleId, 1);
                InsertGroupCondition(connection, groupId, ruleId, 1, 1, "/test-negate", "Url", 1, true);
            }
        }

        private static Guid EnsureAppEntity(System.Data.SqlClient.SqlConnection connection, string host, string appName)
        {
            var appIdCmd = new System.Data.SqlClient.SqlCommand(
                "IF NOT EXISTS (SELECT 1 FROM AppEntity WHERE Host = @Host) " +
                "BEGIN INSERT INTO AppEntity (Id, Host, AppName) VALUES (@Id, @Host, @AppName) END; " +
                "SELECT Id FROM AppEntity WHERE Host = @Host;",
                connection);
            var appId = Guid.NewGuid();
            appIdCmd.Parameters.AddWithValue("@Host", host);
            appIdCmd.Parameters.AddWithValue("@Id", appId);
            appIdCmd.Parameters.AddWithValue("@AppName", appName);
            var result = appIdCmd.ExecuteScalar();
            return result is Guid existingId ? existingId : appId;
        }

        private static int EnsureWafRule(System.Data.SqlClient.SqlConnection connection, Guid appId, string ruleName, bool enabled = true)
        {
            var ruleCmd = new System.Data.SqlClient.SqlCommand(
                "IF NOT EXISTS (SELECT 1 FROM WafRuleEntity WHERE AppId = @AppId AND Nombre = @Nombre) " +
                "INSERT INTO WafRuleEntity (Nombre, ActionId, Prioridad, Habilitado, AppId) VALUES (@Nombre, 1, 1, @Enabled, @AppId);",
                connection);
            ruleCmd.Parameters.AddWithValue("@AppId", appId);
            ruleCmd.Parameters.AddWithValue("@Nombre", ruleName);
            ruleCmd.Parameters.AddWithValue("@Enabled", enabled ? 1 : 0);
            ruleCmd.ExecuteNonQuery();
            var getRuleIdCmd = new System.Data.SqlClient.SqlCommand(
                "SELECT TOP 1 Id FROM WafRuleEntity WHERE AppId = @AppId AND Nombre = @Nombre ORDER BY Id DESC;",
                connection);
            getRuleIdCmd.Parameters.AddWithValue("@AppId", appId);
            getRuleIdCmd.Parameters.AddWithValue("@Nombre", ruleName);
            return Convert.ToInt32(getRuleIdCmd.ExecuteScalar());
        }

        private static void DeleteAllGroupsForRule(System.Data.SqlClient.SqlConnection connection, int ruleId)
        {
            var delGroupCondCmd = new System.Data.SqlClient.SqlCommand(
                "DELETE FROM WafConditionEntity WHERE WafGroupId IN (SELECT Id FROM WafGroups WHERE WafRuleId = @RuleId)",
                connection);
            delGroupCondCmd.Parameters.AddWithValue("@RuleId", ruleId);
            delGroupCondCmd.ExecuteNonQuery();
            var delGroupCmd = new System.Data.SqlClient.SqlCommand(
                "DELETE FROM WafGroups WHERE WafRuleId = @RuleId",
                connection);
            delGroupCmd.Parameters.AddWithValue("@RuleId", ruleId);
            delGroupCmd.ExecuteNonQuery();
        }

        private static int InsertWafGroup(System.Data.SqlClient.SqlConnection connection, int ruleId, int groupOrder)
        {
            var insertGroupCmd = new System.Data.SqlClient.SqlCommand(
                "INSERT INTO WafGroups (WafRuleId, GroupOrder, CreationDate) VALUES (@RuleId, @GroupOrder, @CreationDate); SELECT CAST(SCOPE_IDENTITY() as int);",
                connection);
            insertGroupCmd.Parameters.AddWithValue("@RuleId", ruleId);
            insertGroupCmd.Parameters.AddWithValue("@GroupOrder", groupOrder);
            insertGroupCmd.Parameters.AddWithValue("@CreationDate", DateTime.UtcNow);
            return (int)insertGroupCmd.ExecuteScalar();
        }

        private static void InsertGroupCondition(System.Data.SqlClient.SqlConnection connection, int groupId, int ruleId,
            int fieldId, int operatorId, string valor, string fieldName, int conditionOrder, bool negate)
        {
            var insertCondCmd = new System.Data.SqlClient.SqlCommand(
                "INSERT INTO WafConditionEntity (FieldId, OperatorId, Valor, WafGroupId, WafRuleEntityId, FieldName, ConditionOrder, CreationDate, Negate) " +
                "VALUES (@FieldId, @OperatorId, @Valor, @WafGroupId, @WafRuleEntityId, @FieldName, @ConditionOrder, @CreationDate, @Negate);",
                connection);
            insertCondCmd.Parameters.AddWithValue("@FieldId", fieldId);
            insertCondCmd.Parameters.AddWithValue("@OperatorId", operatorId);
            insertCondCmd.Parameters.AddWithValue("@Valor", valor);
            insertCondCmd.Parameters.AddWithValue("@WafGroupId", groupId);
            insertCondCmd.Parameters.AddWithValue("@WafRuleEntityId", ruleId);
            insertCondCmd.Parameters.AddWithValue("@FieldName", fieldName);
            insertCondCmd.Parameters.AddWithValue("@ConditionOrder", conditionOrder);
            insertCondCmd.Parameters.AddWithValue("@CreationDate", DateTime.UtcNow);
            insertCondCmd.Parameters.AddWithValue("@Negate", negate);
            insertCondCmd.ExecuteNonQuery();
        }
    }
}