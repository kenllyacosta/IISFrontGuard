using IISFrontGuard.Module.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class WafRuleTests
    {
        [Test]
        public void WafRule_CanSetAndGetProperties()
        {
            var appId = Guid.NewGuid();
            var rule = new WafRule
            {
                Id = 1,
                Nombre = "Test Rule",
                ActionId = 2,
                AppId = appId,
                Prioridad = 10,
                Habilitado = true
            };

            Assert.AreEqual(1, rule.Id);
            Assert.AreEqual("Test Rule", rule.Nombre);
            Assert.AreEqual(2, rule.ActionId);
            Assert.AreEqual(appId, rule.AppId);
            Assert.AreEqual(10, rule.Prioridad);
            Assert.IsTrue(rule.Habilitado);
        }

        [Test]
        public void WafRule_GroupsCollection_InitializesAsEmptyList()
        {
            var rule = new WafRule();

            Assert.IsNotNull(rule.Groups);
            Assert.AreEqual(0, rule.Groups.Count);
        }

        [Test]
        public void WafRule_CanAddGroups()
        {
            var rule = new WafRule();
            var group1 = new WafGroup { Id = 1 };
            var group2 = new WafGroup { Id = 2 };

            rule.Groups.Add(group1);
            rule.Groups.Add(group2);

            Assert.AreEqual(2, rule.Groups.Count);
            Assert.AreSame(group1, rule.Groups[0]);
            Assert.AreSame(group2, rule.Groups[1]);
        }

        [Test]
        public void GroupJoin_Or_HasCorrectValue()
        {
            Assert.AreEqual(1, (int)GroupJoin.Or);
        }

        [Test]
        public void ConditionJoin_And_HasCorrectValue()
        {
            Assert.AreEqual(1, (int)ConditionJoin.And);
        }
    }
}
