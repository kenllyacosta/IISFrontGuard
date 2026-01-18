using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Caching;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class CompiledRuleRepositoryTests
    {
        private class MockCacheProvider : ICacheProvider
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

        private class MockWafRuleRepository : IWafRuleRepository
        {
            private readonly List<WafRule> _rules;

            public MockWafRuleRepository(List<WafRule> rules)
            {
                _rules = rules ?? new List<WafRule>();
            }

            public IEnumerable<WafRule> FetchWafRules(string host, string connectionString)
            {
                return _rules;
            }

            public List<WafCondition> FetchWafConditions(int ruleId, string connectionString)
            {
                return new List<WafCondition>();
            }
        }

        [Test]
        public void Constructor_WithNullRepository_ThrowsArgumentNullException()
        {
            var cache = new MockCacheProvider();
            Assert.Throws<ArgumentNullException>(() => new CompiledRuleRepository(null, cache));
        }

        [Test]
        public void Constructor_WithNullCache_ThrowsArgumentNullException()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            Assert.Throws<ArgumentNullException>(() => new CompiledRuleRepository(repo, null));
        }

        [Test]
        public void GetCompiledRules_WithNullHost_ReturnsEmptyList()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules(null, "conn");

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetCompiledRules_CompilesAndCachesRules()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1),
                CreateEnabledRule(2, "Rule2", 2)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(r => r.IsCompiled));
        }

        [Test]
        public void GetCompiledRules_OnlyCompilesEnabledRules()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "EnabledRule", 1),
                CreateDisabledRule(2, "DisabledRule", 2)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Id);
        }

        [Test]
        public void GetCompiledRules_OrdersByPriority()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "LowPriority", 10),
                CreateEnabledRule(2, "HighPriority", 1),
                CreateEnabledRule(3, "MediumPriority", 5)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(2, result[0].Id);
            Assert.AreEqual(3, result[1].Id);
            Assert.AreEqual(1, result[2].Id);
        }

        [Test]
        public void GetCompiledRules_UsesCache_OnSecondCall()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var firstCall = compiledRepo.GetCompiledRules("test.local", "conn");
            var secondCall = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.AreSame(firstCall, secondCall);
        }

        [Test]
        public void InvalidateCache_RemovesBothCaches()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var firstCompiledCall = compiledRepo.GetCompiledRules("test.local", "conn");
            compiledRepo.InvalidateCache("test.local");
            var secondCompiledCall = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.AreNotSame(firstCompiledCall, secondCompiledCall);
        }

        [Test]
        public void InvalidateCache_WithNullHost_DoesNotThrow()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            Assert.DoesNotThrow(() => compiledRepo.InvalidateCache(null));
        }

        private WafRule CreateEnabledRule(int id, string name, int priority)
        {
            return new WafRule
            {
                Id = id,
                Nombre = name,
                ActionId = 2,
                Prioridad = priority,
                Habilitado = true,
                AppId = Guid.NewGuid(),
                Groups = new List<WafGroup>
                {
                    new WafGroup
                    {
                        Id = 1,
                        Conditions = new List<WafCondition>
                        {
                            new WafCondition
                            {
                                FieldId = 7,
                                OperatorId = 1,
                                Valor = "POST",
                                FieldName = "Method"
                            }
                        }
                    }
                },
                Conditions = new List<WafCondition>()
            };
        }

        private WafRule CreateDisabledRule(int id, string name, int priority)
        {
            var rule = CreateEnabledRule(id, name, priority);
            rule.Habilitado = false;
            return rule;
        }
    }
}
