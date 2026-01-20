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

        [Test]
        public void InvalidateCache_WithEmptyHost_DoesNotThrow()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            Assert.DoesNotThrow(() => compiledRepo.InvalidateCache(""));
        }

        [Test]
        public void InvalidateCache_WithWhitespaceHost_DoesNotThrow()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            Assert.DoesNotThrow(() => compiledRepo.InvalidateCache("   "));
        }

        [Test]
        public void InvalidateAllCaches_DoesNotThrow()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            Assert.DoesNotThrow(() => compiledRepo.InvalidateAllCaches());
        }

        [Test]
        public void GetCompiledRules_WithEmptyHost_ReturnsEmptyList()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("", "conn");

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetCompiledRules_WithWhitespaceHost_ReturnsEmptyList()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("   ", "conn");

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetIndexedCompiledRules_WithValidHost_ReturnsIndexedRuleSet()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1),
                CreateEnabledRule(2, "Rule2", 2)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetIndexedCompiledRules("test.local", "conn");

            Assert.IsNotNull(result);
            var stats = result.GetStatistics();
            Assert.AreEqual(2, stats.TotalRules);
        }

        [Test]
        public void GetIndexedCompiledRules_WithNullHost_ReturnsEmptyIndexedRuleSet()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetIndexedCompiledRules(null, "conn");

            Assert.IsNotNull(result);
            var stats = result.GetStatistics();
            Assert.AreEqual(0, stats.TotalRules);
        }

        [Test]
        public void GetIndexedCompiledRules_WithEmptyHost_ReturnsEmptyIndexedRuleSet()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetIndexedCompiledRules("", "conn");

            Assert.IsNotNull(result);
            var stats = result.GetStatistics();
            Assert.AreEqual(0, stats.TotalRules);
        }

        [Test]
        public void GetIndexedCompiledRules_WithWhitespaceHost_ReturnsEmptyIndexedRuleSet()
        {
            var repo = new MockWafRuleRepository(new List<WafRule>());
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetIndexedCompiledRules("   ", "conn");

            Assert.IsNotNull(result);
            var stats = result.GetStatistics();
            Assert.AreEqual(0, stats.TotalRules);
        }

        [Test]
        public void GetIndexedCompiledRules_UsesCache_OnSecondCall()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var firstCall = compiledRepo.GetIndexedCompiledRules("test.local", "conn");
            var secondCall = compiledRepo.GetIndexedCompiledRules("test.local", "conn");

            Assert.AreSame(firstCall, secondCall);
        }

        [Test]
        public void GetIndexedCompiledRules_CacheIsInvalidated_ReturnsNewInstance()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var firstCall = compiledRepo.GetIndexedCompiledRules("test.local", "conn");
            compiledRepo.InvalidateCache("test.local");
            var secondCall = compiledRepo.GetIndexedCompiledRules("test.local", "conn");

            Assert.AreNotSame(firstCall, secondCall);
        }

        [Test]
        public void GetCompiledRules_WithRuleWithoutPriority_DefaultsToZero()
        {
            var rule = CreateEnabledRule(1, "Rule1", 0);
            rule.Prioridad = null;

            var rules = new List<WafRule> { rule };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0, result[0].Priority);
        }

        [Test]
        public void GetCompiledRules_WithCompilationError_ReturnsFallbackRule()
        {
            var rule = CreateMalformedRule(1, "BadRule", 1);

            var rules = new List<WafRule> { rule };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].IsCompiled); // Fallback rule still has Evaluate function
            Assert.IsFalse(result[0].Evaluate(null)); // Fallback always returns false
        }

        [Test]
        public void GetCompiledRules_WithMultipleHosts_CachesSeparately()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result1 = compiledRepo.GetCompiledRules("host1.local", "conn");
            var result2 = compiledRepo.GetCompiledRules("host2.local", "conn");

            Assert.AreNotSame(result1, result2);
        }

        [Test]
        public void GetIndexedCompiledRules_WithMultipleHosts_CachesSeparately()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result1 = compiledRepo.GetIndexedCompiledRules("host1.local", "conn");
            var result2 = compiledRepo.GetIndexedCompiledRules("host2.local", "conn");

            Assert.AreNotSame(result1, result2);
        }

        [Test]
        public void GetCompiledRules_WithNoEnabledRules_ReturnsEmptyList()
        {
            var rules = new List<WafRule>
            {
                CreateDisabledRule(1, "DisabledRule1", 1),
                CreateDisabledRule(2, "DisabledRule2", 2)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GetIndexedCompiledRules_BuildsIndexesCorrectly()
        {
            var rules = new List<WafRule>
            {
                CreateRuleWithMethodCondition(1, "PostRule", 1, "POST"),
                CreateRuleWithMethodCondition(2, "GetRule", 2, "GET")
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetIndexedCompiledRules("test.local", "conn");

            var stats = result.GetStatistics();
            Assert.AreEqual(2, stats.TotalRules);
            Assert.GreaterOrEqual(stats.MethodIndexedRules, 2);
        }

        [Test]
        public void InvalidateCache_RemovesIndexedRuleSetFromCache()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "Rule1", 1)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var firstIndexedCall = compiledRepo.GetIndexedCompiledRules("test.local", "conn");
            compiledRepo.InvalidateCache("test.local");
            var secondIndexedCall = compiledRepo.GetIndexedCompiledRules("test.local", "conn");

            Assert.AreNotSame(firstIndexedCall, secondIndexedCall);
        }

        [Test]
        public void GetCompiledRules_WithMixedEnabledAndDisabledRules_OnlyReturnsEnabled()
        {
            var rules = new List<WafRule>
            {
                CreateEnabledRule(1, "EnabledRule1", 1),
                CreateDisabledRule(2, "DisabledRule", 2),
                CreateEnabledRule(3, "EnabledRule2", 3),
                CreateDisabledRule(4, "DisabledRule2", 4)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var result = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].Id);
            Assert.AreEqual(3, result[1].Id);
        }

        [Test]
        public void Integration_CompleteWorkflow_CompilesAndIndexesRules()
        {
            var rules = new List<WafRule>
            {
                CreateRuleWithMethodCondition(1, "PostRule", 1, "POST"),
                CreateRuleWithPathCondition(2, "ApiRule", 2, "/api"),
                CreateEnabledRule(3, "GenericRule", 3)
            };

            var repo = new MockWafRuleRepository(rules);
            var cache = new MockCacheProvider();
            var compiledRepo = new CompiledRuleRepository(repo, cache);

            var indexed = compiledRepo.GetIndexedCompiledRules("test.local", "conn");
            var compiled = compiledRepo.GetCompiledRules("test.local", "conn");

            Assert.AreEqual(3, compiled.Count);
            var stats = indexed.GetStatistics();
            Assert.AreEqual(3, stats.TotalRules);
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

        private WafRule CreateRuleWithMethodCondition(int id, string name, int priority, string method)
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
                                FieldId = 7, // HTTP Method
                                OperatorId = 1, // Equals
                                Valor = method,
                                FieldName = "Method"
                            }
                        }
                    }
                },
                Conditions = new List<WafCondition>()
            };
        }

        private WafRule CreateRuleWithPathCondition(int id, string name, int priority, string path)
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
                                FieldId = 13, // Path
                                OperatorId = 7, // Starts with
                                Valor = path,
                                FieldName = "Path"
                            }
                        }
                    }
                },
                Conditions = new List<WafCondition>()
            };
        }

        private WafRule CreateMalformedRule(int id, string name, int priority)
        {
            return new WafRule
            {
                Id = id,
                Nombre = name,
                ActionId = 2,
                Prioridad = priority,
                Habilitado = true,
                AppId = Guid.NewGuid(),
                Groups = null, // This will cause compilation issues
                Conditions = null // This will cause compilation issues
            };
        }
    }
}
