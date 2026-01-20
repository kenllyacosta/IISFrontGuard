using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using IISFrontGuard.Module.UnitTests.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class IndexedCompiledRuleSetTests
    {
        private RuleCompiler _compiler;

        [SetUp]
        public void SetUp()
        {
            _compiler = new RuleCompiler();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullRules_CreatesEmptyIndexes()
        {
            var indexedSet = new IndexedCompiledRuleSet(null);

            var stats = indexedSet.GetStatistics();
            Assert.AreEqual(0, stats.TotalRules);
            Assert.AreEqual(0, stats.MethodIndexedRules);
            Assert.AreEqual(0, stats.PathIndexedRules);
            Assert.AreEqual(0, stats.GenericRules);
        }

        [Test]
        public void Constructor_WithEmptyRules_CreatesEmptyIndexes()
        {
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule>());

            var stats = indexedSet.GetStatistics();
            Assert.AreEqual(0, stats.TotalRules);
            Assert.AreEqual(0, stats.MethodIndexedRules);
            Assert.AreEqual(0, stats.PathIndexedRules);
            Assert.AreEqual(0, stats.GenericRules);
        }

        [Test]
        public void Constructor_WithRules_BuildsIndexes()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST"),
                CreateCompiledRuleWithPathCondition("/api"),
                CreateCompiledRuleWithNoConditions()
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);

            var stats = indexedSet.GetStatistics();
            Assert.AreEqual(3, stats.TotalRules);
            Assert.Greater(stats.MethodIndexedRules, 0);
            Assert.Greater(stats.PathIndexedRules, 0);
            Assert.Greater(stats.GenericRules, 0);
        }

        #endregion

        #region GetCandidateRules Tests

        [Test]
        public void GetCandidateRules_WithMatchingMethod_ReturnsCandidates()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST"),
                CreateCompiledRuleWithMethodCondition("GET"),
                CreateCompiledRuleWithMethodCondition("DELETE")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(method: "POST");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
            Assert.IsTrue(candidates.Any(r => r.Name.Contains("POST")));
        }

        [Test]
        public void GetCandidateRules_WithMatchingPath_ReturnsCandidates()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithPathCondition("/api"),
                CreateCompiledRuleWithPathCondition("/admin"),
                CreateCompiledRuleWithPathCondition("/public")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(path: "/api/users");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
            Assert.IsTrue(candidates.Any(r => r.Name.Contains("/api")));
        }

        [Test]
        public void GetCandidateRules_WithMultipleMethodsInList_ReturnsCandidates()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodListCondition("POST,PUT,DELETE"),
                CreateCompiledRuleWithMethodCondition("GET")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var contextPost = TestModelFactory.CreateRequestContext(method: "POST");
            var contextPut = TestModelFactory.CreateRequestContext(method: "PUT");
            var contextDelete = TestModelFactory.CreateRequestContext(method: "DELETE");

            var candidatesPost = indexedSet.GetCandidateRules(contextPost).ToList();
            var candidatesPut = indexedSet.GetCandidateRules(contextPut).ToList();
            var candidatesDelete = indexedSet.GetCandidateRules(contextDelete).ToList();

            Assert.IsNotEmpty(candidatesPost);
            Assert.IsNotEmpty(candidatesPut);
            Assert.IsNotEmpty(candidatesDelete);
        }

        [Test]
        public void GetCandidateRules_WithGenericRules_AlwaysIncludesThem()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithNoConditions(),
                CreateCompiledRuleWithMethodCondition("POST")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(method: "GET");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
            var genericRule = candidates.FirstOrDefault(r => r.Name.Contains("NoConditions"));
            Assert.IsNotNull(genericRule, "Generic rules should always be included");
        }

        [Test]
        public void GetCandidateRules_WithNoMatches_ReturnsAllRules()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST"),
                CreateCompiledRuleWithPathCondition("/admin")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(method: "TRACE", path: "/unknown");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.AreEqual(rules.Count, candidates.Count, "Should return all rules as fallback when no index matches");
        }

        [Test]
        public void GetCandidateRules_OrdersByPriority()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST", priority: 3),
                CreateCompiledRuleWithMethodCondition("POST", priority: 1),
                CreateCompiledRuleWithMethodCondition("POST", priority: 2)
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(method: "POST");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.AreEqual(1, candidates[0].Priority);
            Assert.AreEqual(2, candidates[1].Priority);
            Assert.AreEqual(3, candidates[2].Priority);
        }

        [Test]
        public void GetCandidateRules_WithBothMethodAndPathMatch_CombinesCandidates()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST"),
                CreateCompiledRuleWithPathCondition("/api"),
                CreateCompiledRuleWithMethodAndPathConditions("POST", "/api")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(method: "POST", path: "/api/users");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
            Assert.GreaterOrEqual(candidates.Count, 2);
        }

        [Test]
        public void GetCandidateRules_WithPathWithoutLeadingSlash_HandlesGracefully()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithPathCondition("/api")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(path: "api/users");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.AreEqual(rules.Count, candidates.Count, "Should handle paths without leading slash");
        }

        [Test]
        public void GetCandidateRules_WithEmptyPath_ReturnsAllRules()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithPathCondition("/api")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(path: "");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.AreEqual(rules.Count, candidates.Count);
        }

        [Test]
        public void GetCandidateRules_WithGroupBasedRules_IndexesCorrectly()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithGroupMethodCondition("POST"),
                CreateCompiledRuleWithGroupPathCondition("/api")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(method: "POST", path: "/api/users");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
            Assert.AreEqual(2, candidates.Count);
        }

        [Test]
        public void GetCandidateRules_WithLegacyFlatConditions_IndexesCorrectly()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST"),
                CreateCompiledRuleWithPathCondition("/api")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(method: "POST", path: "/api/users");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
            Assert.AreEqual(2, candidates.Count);
        }

        #endregion

        #region GetStatistics Tests

        [Test]
        public void GetStatistics_WithVariousRules_ReturnsCorrectCounts()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST"),
                CreateCompiledRuleWithMethodCondition("GET"),
                CreateCompiledRuleWithPathCondition("/api"),
                CreateCompiledRuleWithNoConditions()
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var stats = indexedSet.GetStatistics();

            Assert.AreEqual(4, stats.TotalRules);
            Assert.AreEqual(2, stats.MethodIndexedRules);
            Assert.AreEqual(1, stats.PathIndexedRules);
            Assert.AreEqual(1, stats.GenericRules);
            Assert.GreaterOrEqual(stats.MethodIndexSize, 1);
            Assert.GreaterOrEqual(stats.PathIndexSize, 0);
        }

        [Test]
        public void GetStatistics_WithDuplicateIndexes_CountsDistinctRules()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodListCondition("POST,GET"),
                CreateCompiledRuleWithMethodCondition("POST")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var stats = indexedSet.GetStatistics();

            Assert.AreEqual(2, stats.TotalRules);
            Assert.AreEqual(2, stats.MethodIndexedRules);
        }

        [Test]
        public void GetStatistics_ToString_ReturnsFormattedString()
        {
            var stats = new IndexStatistics
            {
                TotalRules = 10,
                MethodIndexedRules = 5,
                PathIndexedRules = 3,
                GenericRules = 2
            };

            var result = stats.ToString();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("10"));
            Assert.IsTrue(result.Contains("5"));
            Assert.IsTrue(result.Contains("3"));
            Assert.IsTrue(result.Contains("2"));
        }

        #endregion

        #region Path Prefix Extraction Tests

        [Test]
        public void PathPrefixExtraction_WithSingleSegment_ReturnsFullPath()
        {
            var rule = CreateCompiledRuleWithPathCondition("/api");
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var context = TestModelFactory.CreateRequestContext(path: "/api");
            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
        }

        [Test]
        public void PathPrefixExtraction_WithMultipleSegments_ReturnsFirstSegment()
        {
            var rule = CreateCompiledRuleWithPathCondition("/api/v1/users");
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var context = TestModelFactory.CreateRequestContext(path: "/api/something");
            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
        }

        [Test]
        public void PathPrefixExtraction_CaseInsensitive_MatchesCorrectly()
        {
            var rule = CreateCompiledRuleWithPathCondition("/API");
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var context = TestModelFactory.CreateRequestContext(path: "/api/users");
            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
        }

        #endregion

        #region Method Index Tests

        [Test]
        public void MethodIndex_WithIsInOperator_IndexesAllMethods()
        {
            var rule = CreateCompiledRuleWithMethodListCondition("POST,PUT,PATCH");
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var stats = indexedSet.GetStatistics();
            Assert.AreEqual(1, stats.MethodIndexedRules);
            Assert.GreaterOrEqual(stats.MethodIndexSize, 3);
        }

        [Test]
        public void MethodIndex_CaseInsensitive_MatchesCorrectly()
        {
            var rule = CreateCompiledRuleWithMethodCondition("post");
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var context = TestModelFactory.CreateRequestContext(method: "POST");
            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
        }

        [Test]
        public void MethodIndex_WithWhitespaceInList_HandlesCorrectly()
        {
            var rule = CreateCompiledRuleWithMethodListCondition("POST , GET , DELETE");
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var context = TestModelFactory.CreateRequestContext(method: "GET");
            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void EdgeCase_RuleWithNullOriginalRule_TreatedAsGeneric()
        {
            var rule = new CompiledRule
            {
                Id = 1,
                Name = "NullOriginal",
                Priority = 1,
                Evaluate = (ctx) => false,
                OriginalRule = null
            };

            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });
            var stats = indexedSet.GetStatistics();

            Assert.AreEqual(1, stats.GenericRules);
        }

        [Test]
        public void EdgeCase_RuleWithNullGroups_DoesNotThrow()
        {
            var rule = CreateCompiledRuleWithNullGroups();
            Assert.DoesNotThrow(() => new IndexedCompiledRuleSet(new List<CompiledRule> { rule }));
        }

        [Test]
        public void EdgeCase_RuleWithNullConditions_DoesNotThrow()
        {
            var rule = CreateCompiledRuleWithNullConditions();
            Assert.DoesNotThrow(() => new IndexedCompiledRuleSet(new List<CompiledRule> { rule }));
        }

        [Test]
        public void EdgeCase_RuleWithEmptyGroups_TreatedAsGeneric()
        {
            var rule = CreateCompiledRuleWithEmptyGroups();
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });
            var stats = indexedSet.GetStatistics();

            Assert.AreEqual(1, stats.GenericRules);
        }

        [Test]
        public void EdgeCase_RuleWithNonMatchingOperatorIds_NotIndexed()
        {
            var rule = CreateCompiledRuleWithNonIndexableConditions();
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });
            var stats = indexedSet.GetStatistics();

            Assert.AreEqual(1, stats.GenericRules);
            Assert.AreEqual(0, stats.MethodIndexedRules);
        }

        [Test]
        public void EdgeCase_PathFieldId14_IndexesCorrectly()
        {
            var rule = CreateCompiledRuleWithPathCondition("/api", fieldId: 14);
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var stats = indexedSet.GetStatistics();
            Assert.AreEqual(1, stats.PathIndexedRules);
        }

        [Test]
        public void EdgeCase_MethodOperatorId13_IndexesCorrectly()
        {
            var rule = CreateCompiledRuleWithMethodCondition("POST", operatorId: 13);
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var stats = indexedSet.GetStatistics();
            Assert.AreEqual(1, stats.MethodIndexedRules);
        }

        [Test]
        public void EdgeCase_MultipleRulesInSameMethodIndex_AllStored()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST", priority: 1),
                CreateCompiledRuleWithMethodCondition("POST", priority: 2),
                CreateCompiledRuleWithMethodCondition("POST", priority: 3)
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(method: "POST");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.AreEqual(3, candidates.Count);
        }

        [Test]
        public void EdgeCase_MultipleRulesInSamePathIndex_AllStored()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithPathCondition("/api", priority: 1),
                CreateCompiledRuleWithPathCondition("/api/v1", priority: 2),
                CreateCompiledRuleWithPathCondition("/api/v2", priority: 3)
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var context = TestModelFactory.CreateRequestContext(path: "/api/users");

            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.GreaterOrEqual(candidates.Count, 3);
        }

        [Test]
        public void EdgeCase_RuleWithGroupNullConditions_DoesNotThrow()
        {
            var rule = CreateCompiledRuleWithGroupNullConditions();
            Assert.DoesNotThrow(() => new IndexedCompiledRuleSet(new List<CompiledRule> { rule }));
        }

        [Test]
        public void EdgeCase_PathWithOnlySlash_HandlesCorrectly()
        {
            var rule = CreateCompiledRuleWithPathCondition("/");
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var context = TestModelFactory.CreateRequestContext(path: "/");
            var candidates = indexedSet.GetCandidateRules(context).ToList();

            Assert.IsNotEmpty(candidates);
        }

        [Test]
        public void EdgeCase_EmptyMethodValue_HandlesCorrectly()
        {
            var rule = CreateCompiledRuleWithMethodListCondition("");
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var stats = indexedSet.GetStatistics();
            Assert.GreaterOrEqual(stats.TotalRules, 1);
        }

        [Test]
        public void Performance_LargeNumberOfRules_IndexesEfficiently()
        {
            var rules = new List<CompiledRule>();
            for (int i = 0; i < 100; i++)
            {
                rules.Add(CreateCompiledRuleWithMethodCondition($"METHOD_{i}", priority: i));
            }

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var stats = indexedSet.GetStatistics();

            Assert.AreEqual(100, stats.TotalRules);
            Assert.AreEqual(100, stats.MethodIndexedRules);
        }

        [Test]
        public void Integration_ComplexRuleWithMultipleGroups_IndexesCorrectly()
        {
            var rule = CreateCompiledRuleWithMultipleGroups();
            var indexedSet = new IndexedCompiledRuleSet(new List<CompiledRule> { rule });

            var stats = indexedSet.GetStatistics();
            Assert.AreEqual(1, stats.TotalRules);
        }

        [Test]
        public void Integration_MixedLegacyAndGroupBasedRules_BothIndexed()
        {
            var rules = new List<CompiledRule>
            {
                CreateCompiledRuleWithMethodCondition("POST"),
                CreateCompiledRuleWithGroupMethodCondition("GET")
            };

            var indexedSet = new IndexedCompiledRuleSet(rules);
            var stats = indexedSet.GetStatistics();

            Assert.AreEqual(2, stats.TotalRules);
            Assert.AreEqual(2, stats.MethodIndexedRules);
        }

        #endregion

        #region Helper Methods

        private CompiledRule CreateCompiledRuleWithMethodCondition(string method, int priority = 1, byte operatorId = 1)
        {
            var rule = new WafRule
            {
                Id = 1,
                Nombre = $"MethodRule_{method}",
                ActionId = 2,
                Prioridad = priority,
                AppId = Guid.NewGuid(),
                Conditions = new List<WafCondition>
                {
                    new WafCondition
                    {
                        Id = 1,
                        FieldId = 7, // HTTP Method
                        OperatorId = operatorId,
                        Valor = method
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithMethodListCondition(string methodList, int priority = 1)
        {
            var rule = new WafRule
            {
                Id = 2,
                Nombre = $"MethodListRule_{methodList}",
                ActionId = 2,
                Prioridad = priority,
                AppId = Guid.NewGuid(),
                Conditions = new List<WafCondition>
                {
                    new WafCondition
                    {
                        Id = 1,
                        FieldId = 7, // HTTP Method
                        OperatorId = 11, // is in list
                        Valor = methodList
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithPathCondition(string path, int priority = 1, byte fieldId = 13)
        {
            var rule = new WafRule
            {
                Id = 3,
                Nombre = $"PathRule_{path}",
                ActionId = 2,
                Prioridad = priority,
                AppId = Guid.NewGuid(),
                Conditions = new List<WafCondition>
                {
                    new WafCondition
                    {
                        Id = 1,
                        FieldId = fieldId, // Path
                        OperatorId = 7, // starts with
                        Valor = path
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithNoConditions()
        {
            var rule = new WafRule
            {
                Id = 4,
                Nombre = "NoConditions",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Conditions = new List<WafCondition>(),
                Groups = new List<WafGroup>()
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithMethodAndPathConditions(string method, string path)
        {
            var rule = new WafRule
            {
                Id = 5,
                Nombre = $"MethodAndPath_{method}_{path}",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Conditions = new List<WafCondition>
                {
                    new WafCondition
                    {
                        Id = 1,
                        FieldId = 7, // HTTP Method
                        OperatorId = 1, // equals
                        Valor = method
                    },
                    new WafCondition
                    {
                        Id = 2,
                        FieldId = 13, // Path
                        OperatorId = 7, // starts with
                        Valor = path
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithGroupMethodCondition(string method)
        {
            var rule = new WafRule
            {
                Id = 6,
                Nombre = $"GroupMethodRule_{method}",
                ActionId = 2,
                Prioridad = 1,
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
                                Id = 1,
                                FieldId = 7, // HTTP Method
                                OperatorId = 1, // equals
                                Valor = method,
                                WafGroupId = 1
                            }
                        }
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithGroupPathCondition(string path)
        {
            var rule = new WafRule
            {
                Id = 7,
                Nombre = $"GroupPathRule_{path}",
                ActionId = 2,
                Prioridad = 1,
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
                                Id = 1,
                                FieldId = 13, // Path
                                OperatorId = 7, // starts with
                                Valor = path,
                                WafGroupId = 1
                            }
                        }
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithNullGroups()
        {
            var rule = new WafRule
            {
                Id = 8,
                Nombre = "NullGroups",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Groups = null,
                Conditions = new List<WafCondition>()
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithNullConditions()
        {
            var rule = new WafRule
            {
                Id = 9,
                Nombre = "NullConditions",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Conditions = null,
                Groups = new List<WafGroup>()
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithEmptyGroups()
        {
            var rule = new WafRule
            {
                Id = 10,
                Nombre = "EmptyGroups",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Groups = new List<WafGroup>(),
                Conditions = new List<WafCondition>()
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithNonIndexableConditions()
        {
            var rule = new WafRule
            {
                Id = 11,
                Nombre = "NonIndexable",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Conditions = new List<WafCondition>
                {
                    new WafCondition
                    {
                        Id = 1,
                        FieldId = 1, // Cookie (not indexable)
                        OperatorId = 3, // contains (not indexable for method)
                        Valor = "test"
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithGroupNullConditions()
        {
            var rule = new WafRule
            {
                Id = 12,
                Nombre = "GroupWithNullConditions",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Groups = new List<WafGroup>
                {
                    new WafGroup
                    {
                        Id = 1,
                        Conditions = null
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        private CompiledRule CreateCompiledRuleWithMultipleGroups()
        {
            var rule = new WafRule
            {
                Id = 13,
                Nombre = "MultipleGroups",
                ActionId = 2,
                Prioridad = 1,
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
                                Id = 1,
                                FieldId = 7,
                                OperatorId = 1,
                                Valor = "POST",
                                WafGroupId = 1
                            }
                        }
                    },
                    new WafGroup
                    {
                        Id = 2,
                        Conditions = new List<WafCondition>
                        {
                            new WafCondition
                            {
                                Id = 2,
                                FieldId = 13,
                                OperatorId = 7,
                                Valor = "/api",
                                WafGroupId = 2
                            }
                        }
                    }
                }
            };

            return _compiler.CompileRule(rule);
        }

        #endregion
    }
}
