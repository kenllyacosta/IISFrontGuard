using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using IISFrontGuard.Module.UnitTests.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class RuleCompilerTests
    {
        private RuleCompiler _compiler;

        [SetUp]
        public void SetUp()
        {
            _compiler = new RuleCompiler();
        }

        [Test]
        public void CompileRule_WithNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _compiler.CompileRule(null));
        }

        [Test]
        public void CompileRule_WithNoConditions_ReturnsRuleThatAlwaysReturnsFalse()
        {
            var rule = new WafRule
            {
                Id = 1,
                Nombre = "EmptyRule",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Conditions = new List<WafCondition>(),
                Groups = new List<WafGroup>()
            };

            var compiled = _compiler.CompileRule(rule);

            Assert.IsNotNull(compiled);
            Assert.AreEqual(1, compiled.Id);
            Assert.AreEqual("EmptyRule", compiled.Name);
            Assert.AreEqual(2, compiled.ActionId);
            Assert.AreEqual(1, compiled.Priority);
            Assert.IsTrue(compiled.IsCompiled);
            Assert.IsFalse(compiled.Evaluate(CreateMockRequestContext()));
        }

        [Test]
        public void CompileRule_WithGroupBasedSchema_CompilesCorrectly()
        {
            var rule = new WafRule
            {
                Id = 1,
                Nombre = "GroupRule",
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
                                FieldId = 7, // method
                                OperatorId = 1, // equals
                                Valor = "POST",
                                FieldName = "Method",
                                WafGroupId = 1
                            }
                        }
                    }
                },
                Conditions = new List<WafCondition>()
            };

            var compiled = _compiler.CompileRule(rule);

            Assert.IsNotNull(compiled);
            Assert.IsTrue(compiled.IsCompiled);

            var contextPost = CreateMockRequestContext("POST", "/test");
            var contextGet = CreateMockRequestContext("GET", "/test");

            Assert.IsTrue(compiled.Evaluate(contextPost));
            Assert.IsFalse(compiled.Evaluate(contextGet));
        }

        [Test]
        public void CompileRule_WithMultipleGroups_UsesOrLogic()
        {
            var rule = new WafRule
            {
                Id = 1,
                Nombre = "MultiGroupRule",
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
                            new WafCondition { FieldId = 7, OperatorId = 1, Valor = "POST", FieldName = "Method", WafGroupId = 1 }
                        }
                    },
                    new WafGroup
                    {
                        Id = 2,
                        Conditions = new List<WafCondition>
                        {
                            new WafCondition { FieldId = 7, OperatorId = 1, Valor = "PUT", FieldName = "Method", WafGroupId = 2 }
                        }
                    }
                },
                Conditions = new List<WafCondition>()
            };

            var compiled = _compiler.CompileRule(rule);

            var contextPost = CreateMockRequestContext("POST", "/test");
            var contextPut = CreateMockRequestContext("PUT", "/test");
            var contextGet = CreateMockRequestContext("GET", "/test");

            Assert.IsTrue(compiled.Evaluate(contextPost));
            Assert.IsTrue(compiled.Evaluate(contextPut));
            Assert.IsFalse(compiled.Evaluate(contextGet));
        }

        [Test]
        public void CompileRule_WithAndConditionsInGroup_RequiresAllToMatch()
        {
            var rule = new WafRule
            {
                Id = 1,
                Nombre = "AndRule",
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
                            new WafCondition { FieldId = 7, OperatorId = 1, Valor = "POST", FieldName = "Method", WafGroupId = 1 },
                            new WafCondition { FieldId = 13, OperatorId = 7, Valor = "/api", FieldName = "Path", WafGroupId = 1 }
                        }
                    }
                },
                Conditions = new List<WafCondition>()
            };

            var compiled = _compiler.CompileRule(rule);

            var contextMatch = CreateMockRequestContext("POST", "/api/users");
            var contextMethodMismatch = CreateMockRequestContext("GET", "/api/users");
            var contextPathMismatch = CreateMockRequestContext("POST", "/web/users");

            Assert.IsTrue(compiled.Evaluate(contextMatch));
            Assert.IsFalse(compiled.Evaluate(contextMethodMismatch));
            Assert.IsFalse(compiled.Evaluate(contextPathMismatch));
        }

        [Test]
        public void CompileRule_WithNegatedCondition_InvertsResult()
        {
            var rule = new WafRule
            {
                Id = 1,
                Nombre = "NegateRule",
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
                                FieldId = 7,
                                OperatorId = 1,
                                Valor = "POST",
                                FieldName = "Method",
                                WafGroupId = 1,
                                Negate = true
                            }
                        }
                    }
                },
                Conditions = new List<WafCondition>()
            };

            var compiled = _compiler.CompileRule(rule);

            var contextPost = CreateMockRequestContext("POST", "/test");
            var contextGet = CreateMockRequestContext("GET", "/test");

            Assert.IsFalse(compiled.Evaluate(contextPost));
            Assert.IsTrue(compiled.Evaluate(contextGet));
        }

        [Test]
        public void CompileRule_WithLegacyFlatSchema_CompilesCorrectly()
        {
            var rule = new WafRule
            {
                Id = 1,
                Nombre = "LegacyRule",
                ActionId = 2,
                Prioridad = 1,
                AppId = Guid.NewGuid(),
                Groups = new List<WafGroup>(),
                Conditions = new List<WafCondition>
                {
                    new WafCondition
                    {
                        Id = 1,
                        FieldId = 7,
                        OperatorId = 1,
                        Valor = "POST",
                        FieldName = "Method",
                        LogicOperator = 1, // AND
                        WafRuleEntityId = 1
                    }
                }
            };

            var compiled = _compiler.CompileRule(rule);

            var contextPost = CreateMockRequestContext("POST", "/test");
            var contextGet = CreateMockRequestContext("GET", "/test");

            Assert.IsTrue(compiled.Evaluate(contextPost));
            Assert.IsFalse(compiled.Evaluate(contextGet));
        }

        [Test]
        public void CompileCondition_EqualsOperator_WorksCorrectly()
        {
            var rule = CreateRuleWithSingleCondition(7, 1, "GET");

            var compiled = _compiler.CompileRule(rule);

            Assert.IsTrue(compiled.Evaluate(CreateMockRequestContext("GET", "/test")));
            Assert.IsFalse(compiled.Evaluate(CreateMockRequestContext("POST", "/test")));
        }

        [Test]
        public void CompileCondition_NotEqualsOperator_WorksCorrectly()
        {
            var rule = CreateRuleWithSingleCondition(7, 2, "GET");

            var compiled = _compiler.CompileRule(rule);

            Assert.IsFalse(compiled.Evaluate(CreateMockRequestContext("GET", "/test")));
            Assert.IsTrue(compiled.Evaluate(CreateMockRequestContext("POST", "/test")));
        }

        [Test]
        public void CompileCondition_ContainsOperator_WorksCorrectly()
        {
            var rule = CreateRuleWithSingleCondition(13, 3, "api");

            var compiled = _compiler.CompileRule(rule);

            Assert.IsTrue(compiled.Evaluate(CreateMockRequestContext("GET", "/api/users")));
            Assert.IsFalse(compiled.Evaluate(CreateMockRequestContext("GET", "/web/users")));
        }

        [Test]
        public void CompileCondition_StartsWithOperator_WorksCorrectly()
        {
            var rule = CreateRuleWithSingleCondition(13, 7, "/api");

            var compiled = _compiler.CompileRule(rule);

            Assert.IsTrue(compiled.Evaluate(CreateMockRequestContext("GET", "/api/users")));
            Assert.IsFalse(compiled.Evaluate(CreateMockRequestContext("GET", "/web/api")));
        }

        [Test]
        public void CompileCondition_EndsWithOperator_WorksCorrectly()
        {
            var rule = CreateRuleWithSingleCondition(13, 9, ".php");

            var compiled = _compiler.CompileRule(rule);

            Assert.IsTrue(compiled.Evaluate(CreateMockRequestContext("GET", "/admin.php")));
            Assert.IsFalse(compiled.Evaluate(CreateMockRequestContext("GET", "/admin.html")));
        }

        [Test]
        public void CompileCondition_IsPresentOperator_WorksCorrectly()
        {
            var rule = CreateRuleWithSingleCondition(9, 21, "");

            var compiled = _compiler.CompileRule(rule);

            var contextWithUA = CreateMockRequestContext("GET", "/test", userAgent: "Mozilla/5.0");
            var contextWithoutUA = CreateMockRequestContext("GET", "/test", userAgent: "");

            Assert.IsTrue(compiled.Evaluate(contextWithUA));
            Assert.IsFalse(compiled.Evaluate(contextWithoutUA));
        }

        [Test]
        public void CompileCondition_IsNotPresentOperator_WorksCorrectly()
        {
            var rule = CreateRuleWithSingleCondition(9, 22, "");

            var compiled = _compiler.CompileRule(rule);

            var contextWithUA = CreateMockRequestContext("GET", "/test", userAgent: "Mozilla/5.0");
            var contextWithoutUA = CreateMockRequestContext("GET", "/test", userAgent: "");

            Assert.IsFalse(compiled.Evaluate(contextWithUA));
            Assert.IsTrue(compiled.Evaluate(contextWithoutUA));
        }

        [Test]
        public void CompileCondition_CaseInsensitive_WorksCorrectly()
        {
            var rule = CreateRuleWithSingleCondition(7, 1, "POST");

            var compiled = _compiler.CompileRule(rule);

            Assert.IsTrue(compiled.Evaluate(CreateMockRequestContext("POST", "/test")));
            Assert.IsTrue(compiled.Evaluate(CreateMockRequestContext("post", "/test")));
            Assert.IsTrue(compiled.Evaluate(CreateMockRequestContext("Post", "/test")));
        }

        private WafRule CreateRuleWithSingleCondition(byte fieldId, byte operatorId, string valor)
        {
            return new WafRule
            {
                Id = 1,
                Nombre = "TestRule",
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
                                FieldId = fieldId,
                                OperatorId = operatorId,
                                Valor = valor,
                                FieldName = "TestField",
                                WafGroupId = 1
                            }
                        }
                    }
                },
                Conditions = new List<WafCondition>()
            };
        }

        private static RequestContext CreateMockRequestContext(string method = "GET", string path = "/", string userAgent = "Test")
        {
            return TestModelFactory.CreateRequestContext(method: method, path: path, userAgent: userAgent);
        }
    }
}
