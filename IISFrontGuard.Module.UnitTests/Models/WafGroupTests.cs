using IISFrontGuard.Module.Models;
using NUnit.Framework;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class WafGroupTests
    {
        [Test]
        public void WafGroup_CanSetAndGetId()
        {
            var group = new WafGroup { Id = 42 };

            Assert.AreEqual(42, group.Id);
        }

        [Test]
        public void WafGroup_ConditionsCollection_InitializesAsEmptyList()
        {
            var group = new WafGroup();

            Assert.IsNotNull(group.Conditions);
            Assert.AreEqual(0, group.Conditions.Count);
        }

        [Test]
        public void WafGroup_CanAddConditions()
        {
            var group = new WafGroup();
            var condition1 = new WafCondition { Id = 1, FieldId = 3, OperatorId = 1 };
            var condition2 = new WafCondition { Id = 2, FieldId = 2, OperatorId = 3 };

            group.Conditions.Add(condition1);
            group.Conditions.Add(condition2);

            Assert.AreEqual(2, group.Conditions.Count);
            Assert.AreSame(condition1, group.Conditions[0]);
            Assert.AreSame(condition2, group.Conditions[1]);
        }
    }
}
