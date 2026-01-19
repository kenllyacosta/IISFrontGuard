using IISFrontGuard.Module.Models;
using NUnit.Framework;
using System;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class ChallengeFailureInfoTests
    {
        [Test]
        public void ChallengeFailureInfo_CanSetAndGetFirstFailure()
        {
            var firstFailure = DateTime.UtcNow;
            var info = new ChallengeFailureInfo { FirstFailure = firstFailure };

            Assert.AreEqual(firstFailure, info.FirstFailure);
        }

        [Test]
        public void ChallengeFailureInfo_CanSetAndGetFailureCount()
        {
            var info = new ChallengeFailureInfo { FailureCount = 5 };

            Assert.AreEqual(5, info.FailureCount);
        }

        [Test]
        public void ChallengeFailureInfo_DefaultFailureCount_IsZero()
        {
            var info = new ChallengeFailureInfo();

            Assert.AreEqual(0, info.FailureCount);
        }

        [Test]
        public void ChallengeFailureInfo_CanIncrementFailureCount()
        {
            var info = new ChallengeFailureInfo { FailureCount = 3 };
            
            info.FailureCount++;

            Assert.AreEqual(4, info.FailureCount);
        }
    }
}
