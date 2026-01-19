using IISFrontGuard.Module.Models;
using NUnit.Framework;
using System;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class SecurityEventTests
    {
        [Test]
        public void SecurityEvent_CanSetAndGetAllProperties()
        {
            var timestamp = DateTime.UtcNow;
            var additionalData = new { CustomField = "value" };

            var secEvent = new SecurityEvent
            {
                EventType = SecurityEventTypes.RequestBlocked,
                Severity = SecurityEventSeverity.High,
                Timestamp = timestamp,
                RayId = "abc123",
                ClientIp = "192.168.1.1",
                HostName = "example.com",
                UserAgent = "Mozilla/5.0",
                Url = "https://example.com/api",
                HttpMethod = "POST",
                RuleId = 42,
                RuleName = "SQL Injection Rule",
                CountryCode = "US",
                Description = "Blocked malicious request",
                AdditionalData = additionalData
            };

            Assert.AreEqual(SecurityEventTypes.RequestBlocked, secEvent.EventType);
            Assert.AreEqual(SecurityEventSeverity.High, secEvent.Severity);
            Assert.AreEqual(timestamp, secEvent.Timestamp);
            Assert.AreEqual("abc123", secEvent.RayId);
            Assert.AreEqual("192.168.1.1", secEvent.ClientIp);
            Assert.AreEqual("example.com", secEvent.HostName);
            Assert.AreEqual("Mozilla/5.0", secEvent.UserAgent);
            Assert.AreEqual("https://example.com/api", secEvent.Url);
            Assert.AreEqual("POST", secEvent.HttpMethod);
            Assert.AreEqual(42, secEvent.RuleId);
            Assert.AreEqual("SQL Injection Rule", secEvent.RuleName);
            Assert.AreEqual("US", secEvent.CountryCode);
            Assert.AreEqual("Blocked malicious request", secEvent.Description);
            Assert.AreSame(additionalData, secEvent.AdditionalData);
        }

        [Test]
        public void SecurityEvent_RuleId_CanBeNull()
        {
            var secEvent = new SecurityEvent { RuleId = null };

            Assert.IsNull(secEvent.RuleId);
        }

        [Test]
        public void SecurityEvent_AdditionalData_CanBeNull()
        {
            var secEvent = new SecurityEvent { AdditionalData = null };

            Assert.IsNull(secEvent.AdditionalData);
        }
    }
}
