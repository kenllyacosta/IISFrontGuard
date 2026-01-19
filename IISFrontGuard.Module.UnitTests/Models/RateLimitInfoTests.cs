using IISFrontGuard.Module.Models;
using NUnit.Framework;
using System;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class RateLimitInfoTests
    {
        [Test]
        public void RateLimitInfo_CanSetAndGetRequestCount()
        {
            var info = new RateLimitInfo { RequestCount = 10 };

            Assert.AreEqual(10, info.RequestCount);
        }

        [Test]
        public void RateLimitInfo_CanSetAndGetWindowStart()
        {
            var windowStart = DateTime.UtcNow;
            var info = new RateLimitInfo { WindowStart = windowStart };

            Assert.AreEqual(windowStart, info.WindowStart);
        }

        [Test]
        public void RateLimitInfo_DefaultRequestCount_IsZero()
        {
            var info = new RateLimitInfo();

            Assert.AreEqual(0, info.RequestCount);
        }

        [Test]
        public void RateLimitInfo_CanIncrementRequestCount()
        {
            var info = new RateLimitInfo { RequestCount = 5 };
            
            info.RequestCount++;
            
            Assert.AreEqual(6, info.RequestCount);
        }
    }
}
