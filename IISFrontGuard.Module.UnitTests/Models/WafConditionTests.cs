using IISFrontGuard.Module.Models;
using NUnit.Framework;
using System;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class WafConditionTests
    {
        [Test]
        public void WafCondition_CanSetAndGetAllProperties()
        {
            var creationDate = DateTime.UtcNow;
            var condition = new WafCondition
            {
                Id = 1,
                FieldId = 3,
                OperatorId = 1,
                Valor = "192.168.1.1",
                FieldName = "CustomHeader",
                Negate = true,
                CreationDate = creationDate,
                WafGroupId = 10
            };

            Assert.AreEqual(1, condition.Id);
            Assert.AreEqual(3, condition.FieldId);
            Assert.AreEqual(1, condition.OperatorId);
            Assert.AreEqual("192.168.1.1", condition.Valor);
            Assert.AreEqual("CustomHeader", condition.FieldName);
            Assert.IsTrue(condition.Negate);
            Assert.AreEqual(creationDate, condition.CreationDate);
            Assert.AreEqual(10, condition.WafGroupId);
        }

        [Test]
        public void WafCondition_DefaultNegate_IsFalse()
        {
            var condition = new WafCondition();

            Assert.IsFalse(condition.Negate);
        }

        [Test]
        public void WafCondition_WafGroupId_CanBeNull()
        {
            var condition = new WafCondition { WafGroupId = null };

            Assert.IsNull(condition.WafGroupId);
        }

        [Test]
        public void WafCondition_FieldName_CanBeNull()
        {
            var condition = new WafCondition { FieldName = null };

            Assert.IsNull(condition.FieldName);
        }
    }
}
