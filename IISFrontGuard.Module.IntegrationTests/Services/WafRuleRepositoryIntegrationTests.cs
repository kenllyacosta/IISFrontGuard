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
                throw new NotImplementedException();
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
    }
}