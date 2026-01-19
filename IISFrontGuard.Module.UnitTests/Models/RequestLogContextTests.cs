using IISFrontGuard.Module.Models;
using NUnit.Framework;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class RequestLogContextTests
    {
        [Test]
        public void RequestLogContext_CanSetAndGetRayId()
        {
            var context = new RequestLogContext { RayId = "test-ray-123" };

            Assert.AreEqual("test-ray-123", context.RayId);
        }

        [Test]
        public void RequestLogContext_CanSetAndGetRuleTriggered()
        {
            var context = new RequestLogContext { RuleTriggered = 42 };

            Assert.AreEqual(42, context.RuleTriggered);
        }

        [Test]
        public void RequestLogContext_CanSetAndGetConnectionString()
        {
            var context = new RequestLogContext { ConnectionString = "Data Source=..." };

            Assert.AreEqual("Data Source=...", context.ConnectionString);
        }

        [Test]
        public void RequestLogContext_CanSetAndGetIso2()
        {
            var context = new RequestLogContext { Iso2 = "US" };

            Assert.AreEqual("US", context.Iso2);
        }

        [Test]
        public void RequestLogContext_CanSetAndGetActionId()
        {
            var context = new RequestLogContext { ActionId = 2 };

            Assert.AreEqual(2, context.ActionId);
        }

        [Test]
        public void RequestLogContext_CanSetAndGetAppId()
        {
            var context = new RequestLogContext { AppId = "test-app-id" };

            Assert.AreEqual("test-app-id", context.AppId);
        }
    }
}
