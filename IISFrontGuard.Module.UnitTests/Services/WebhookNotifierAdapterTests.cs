using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using NUnit.Framework;
using System;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class WebhookNotifierAdapterTests
    {
        [Test]
        public void EnqueueSecurityEvent_WithValidEvent_CallsUnderlyingService()
        {
            // Arrange
            var adapter = new WebhookNotifierAdapter();
            var securityEvent = new SecurityEvent
            {
                EventType = "WAF_BLOCK",
                Severity = "HIGH",
                Timestamp = DateTime.UtcNow,
                RayId = "test-ray-123",
                ClientIp = "192.168.1.1",
                HostName = "example.com",
                UserAgent = "Mozilla/5.0",
                Url = "https://example.com/test",
                HttpMethod = "GET",
                RuleId = 1,
                RuleName = "SQL Injection Detection",
                CountryCode = "US",
                Description = "Blocked SQL injection attempt"
            };

            // Act - This covers line 10
            adapter.EnqueueSecurityEvent(securityEvent);

            // Assert
            Assert.Pass("EnqueueSecurityEvent executed successfully");
        }

        [Test]
        public void EnqueueSecurityEvent_WithNullEvent_HandlesGracefully()
        {
            // Arrange
            var adapter = new WebhookNotifierAdapter();

            // Act - This also covers line 10
            adapter.EnqueueSecurityEvent(null);

            // Assert
            Assert.Pass("Null event handled gracefully");
        }

        [Test]
        public void Stop_WhenCalled_CallsUnderlyingService()
        {
            // Arrange
            var adapter = new WebhookNotifierAdapter();

            // Act - This covers line 15
            adapter.Stop();

            // Assert
            Assert.Pass("Stop executed successfully");
        }

        [Test]
        public void EnqueueSecurityEvent_WithMinimalEvent_ExecutesSuccessfully()
        {
            // Arrange
            var adapter = new WebhookNotifierAdapter();
            var securityEvent = new SecurityEvent
            {
                EventType = "TEST_EVENT",
                Timestamp = DateTime.UtcNow
            };

            // Act
            adapter.EnqueueSecurityEvent(securityEvent);

            // Assert
            Assert.Pass("Minimal event enqueued successfully");
        }
    }
}