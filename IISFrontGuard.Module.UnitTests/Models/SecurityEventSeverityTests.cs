using IISFrontGuard.Module.Models;
using NUnit.Framework;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class SecurityEventSeverityTests
    {
        [Test]
        public void SecurityEventSeverity_Critical_HasCorrectValue()
        {
            Assert.AreEqual("critical", SecurityEventSeverity.Critical);
        }

        [Test]
        public void SecurityEventSeverity_High_HasCorrectValue()
        {
            Assert.AreEqual("high", SecurityEventSeverity.High);
        }

        [Test]
        public void SecurityEventSeverity_Medium_HasCorrectValue()
        {
            Assert.AreEqual("medium", SecurityEventSeverity.Medium);
        }

        [Test]
        public void SecurityEventSeverity_Low_HasCorrectValue()
        {
            Assert.AreEqual("low", SecurityEventSeverity.Low);
        }

        [Test]
        public void SecurityEventSeverity_Info_HasCorrectValue()
        {
            Assert.AreEqual("info", SecurityEventSeverity.Info);
        }
    }
}
