using IISFrontGuard.Module.Models;
using NUnit.Framework;
using System;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class ChallengeContextTests
    {
        [Test]
        public void ChallengeContext_CanSetAndGetToken()
        {
            var context = new ChallengeContext { Token = "test-token-123" };

            Assert.AreEqual("test-token-123", context.Token);
        }

        [Test]
        public void ChallengeContext_CanSetAndGetKey()
        {
            var context = new ChallengeContext { Key = "encryption-key" };

            Assert.AreEqual("encryption-key", context.Key);
        }

        [Test]
        public void ChallengeContext_CanSetAndGetLogContext()
        {
            var logContext = new RequestLogContext();
            var context = new ChallengeContext { LogContext = logContext };

            Assert.AreSame(logContext, context.LogContext);
        }

        [Test]
        public void ChallengeContext_CanSetAndGetHtmlGenerator()
        {
            Func<string, string, string, string> generator = 
                (domain, rayId, csrf) => $"<html>{domain}-{rayId}-{csrf}</html>";

            var context = new ChallengeContext { HtmlGenerator = generator };

            Assert.AreSame(generator, context.HtmlGenerator);
            var html = context.HtmlGenerator("example.com", "ray123", "csrf456");
            Assert.AreEqual("<html>example.com-ray123-csrf456</html>", html);
        }

        [Test]
        public void ChallengeContext_Request_CanBeNull()
        {
            var context = new ChallengeContext { Request = null };

            Assert.IsNull(context.Request);
        }

        [Test]
        public void ChallengeContext_Response_CanBeNull()
        {
            var context = new ChallengeContext { Response = null };

            Assert.IsNull(context.Response);
        }
    }
}
