using IISFrontGuard.Module.Models;
using NUnit.Framework;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class SecurityEventTypesTests
    {
        [Test]
        public void SecurityEventTypes_SQLInjectionAttempt_HasCorrectValue()
        {
            Assert.AreEqual("sql_injection_attempt", SecurityEventTypes.SQLInjectionAttempt);
        }

        [Test]
        public void SecurityEventTypes_XSSAttempt_HasCorrectValue()
        {
            Assert.AreEqual("xss_attempt", SecurityEventTypes.XSSAttempt);
        }

        [Test]
        public void SecurityEventTypes_PathTraversalAttempt_HasCorrectValue()
        {
            Assert.AreEqual("path_traversal_attempt", SecurityEventTypes.PathTraversalAttempt);
        }

        [Test]
        public void SecurityEventTypes_CommandInjectionAttempt_HasCorrectValue()
        {
            Assert.AreEqual("command_injection_attempt", SecurityEventTypes.CommandInjectionAttempt);
        }

        [Test]
        public void SecurityEventTypes_RateLimitExceeded_HasCorrectValue()
        {
            Assert.AreEqual("rate_limit_exceeded", SecurityEventTypes.RateLimitExceeded);
        }

        [Test]
        public void SecurityEventTypes_DistributedAttack_HasCorrectValue()
        {
            Assert.AreEqual("distributed_attack", SecurityEventTypes.DistributedAttack);
        }

        [Test]
        public void SecurityEventTypes_BruteForceAttempt_HasCorrectValue()
        {
            Assert.AreEqual("brute_force_attempt", SecurityEventTypes.BruteForceAttempt);
        }

        [Test]
        public void SecurityEventTypes_SuspiciousUserAgent_HasCorrectValue()
        {
            Assert.AreEqual("suspicious_user_agent", SecurityEventTypes.SuspiciousUserAgent);
        }

        [Test]
        public void SecurityEventTypes_BotDetected_HasCorrectValue()
        {
            Assert.AreEqual("bot_detected", SecurityEventTypes.BotDetected);
        }

        [Test]
        public void SecurityEventTypes_AnomalousTraffic_HasCorrectValue()
        {
            Assert.AreEqual("anomalous_traffic", SecurityEventTypes.AnomalousTraffic);
        }

        [Test]
        public void SecurityEventTypes_RequestBlocked_HasCorrectValue()
        {
            Assert.AreEqual("request_blocked", SecurityEventTypes.RequestBlocked);
        }

        [Test]
        public void SecurityEventTypes_ChallengeIssued_HasCorrectValue()
        {
            Assert.AreEqual("challenge_issued", SecurityEventTypes.ChallengeIssued);
        }

        [Test]
        public void SecurityEventTypes_MultipleChallengeFails_HasCorrectValue()
        {
            Assert.AreEqual("multiple_challenge_fails", SecurityEventTypes.MultipleChallengeFails);
        }

        [Test]
        public void SecurityEventTypes_InvalidToken_HasCorrectValue()
        {
            Assert.AreEqual("invalid_token", SecurityEventTypes.InvalidToken);
        }

        [Test]
        public void SecurityEventTypes_TokenReplayAttempt_HasCorrectValue()
        {
            Assert.AreEqual("token_replay_attempt", SecurityEventTypes.TokenReplayAttempt);
        }

        [Test]
        public void SecurityEventTypes_CSRFTokenMismatch_HasCorrectValue()
        {
            Assert.AreEqual("csrf_token_mismatch", SecurityEventTypes.CSRFTokenMismatch);
        }

        [Test]
        public void SecurityEventTypes_UnexpectedGeoLocation_HasCorrectValue()
        {
            Assert.AreEqual("unexpected_geo_location", SecurityEventTypes.UnexpectedGeoLocation);
        }

        [Test]
        public void SecurityEventTypes_HighRiskCountry_HasCorrectValue()
        {
            Assert.AreEqual("high_risk_country", SecurityEventTypes.HighRiskCountry);
        }
    }
}
