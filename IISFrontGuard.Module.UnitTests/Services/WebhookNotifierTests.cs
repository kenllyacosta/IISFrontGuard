using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class WebhookNotifierTests
    {
        private string _testLogPath;
        private HttpListener _mockWebhookServer;
        private string _webhookUrl;
        private bool _serverRunning;
        private readonly Dictionary<string, string> _receivedCustomHeaders = new Dictionary<string, string>();

        [SetUp]
        public void SetUp()
        {
            _testLogPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.log");
            
            // Set up a mock HTTP server for webhook testing
            _mockWebhookServer = new HttpListener();
            _webhookUrl = "http://localhost:8765/webhook/";
            _mockWebhookServer.Prefixes.Add(_webhookUrl);
            // Also add a failing endpoint prefix used by tests
            _mockWebhookServer.Prefixes.Add("http://localhost:8765/fail/");
            
            try
            {
                _mockWebhookServer.Start();
                _serverRunning = true;
                // Start listener in background
                Task.Run(() => HandleWebhookRequests());
            }
            catch
            {
                _serverRunning = false;
            }

            // Reset configuration
        }

        [TearDown]
        public void TearDown()
        {
            if (_mockWebhookServer != null && _mockWebhookServer.IsListening)
            {
                _serverRunning = false;
                _mockWebhookServer.Stop();
                _mockWebhookServer.Close();
            }

            // Reset static state
            ResetWebhookNotifierState();
        }

        [Test]
        public void GetWebhookUrls_WithSemicolonDelimitedUrls_ParsesCorrectly()
        {
            // Arrange
            SetAppConfig("IISFrontGuard.Webhook.Url", "http://webhook1.com;http://webhook2.com;http://webhook3.com");

            // Act
            var urls = WebhookNotifier.GetWebhookUrls();

            // Assert
            Assert.AreEqual(3, urls.Length);
            Assert.AreEqual("http://webhook1.com", urls[0]);
            Assert.AreEqual("http://webhook2.com", urls[1]);
            Assert.AreEqual("http://webhook3.com", urls[2]);
        }

        [Test]
        public void GetWebhookUrls_WithCommaDelimitedUrls_ParsesCorrectly()
        {
            // Arrange
            SetAppConfig("IISFrontGuard.Webhook.Url", "http://webhook1.com,http://webhook2.com,http://webhook3.com");

            // Act
            var urls = WebhookNotifier.GetWebhookUrls();

            // Assert
            Assert.AreEqual(3, urls.Length);
            Assert.AreEqual("http://webhook1.com", urls[0]);
            Assert.AreEqual("http://webhook2.com", urls[1]);
            Assert.AreEqual("http://webhook3.com", urls[2]);
        }

        [Test]
        public void GetWebhookUrls_WithMixedDelimiters_ParsesCorrectly()
        {
            // Arrange
            SetAppConfig("IISFrontGuard.Webhook.Url", "http://webhook1.com;http://webhook2.com,http://webhook3.com");

            // Act
            var urls = WebhookNotifier.GetWebhookUrls();

            // Assert
            Assert.AreEqual(3, urls.Length);
            Assert.AreEqual("http://webhook1.com", urls[0]);
            Assert.AreEqual("http://webhook2.com", urls[1]);
            Assert.AreEqual("http://webhook3.com", urls[2]);
        }

        [Test]
        public void GetWebhookUrls_WithExtraWhitespace_TrimsCorrectly()
        {
            // Arrange
            SetAppConfig("IISFrontGuard.Webhook.Url", "  http://webhook1.com  ;  http://webhook2.com  ");

            // Act
            var urls = WebhookNotifier.GetWebhookUrls();

            // Assert
            Assert.AreEqual(2, urls.Length);
            Assert.AreEqual("http://webhook1.com", urls[0]);
            Assert.AreEqual("http://webhook2.com", urls[1]);
        }

        [Test]
        public void GetWebhookUrls_WithEmptyEntries_FiltersCorrectly()
        {
            // Arrange
            SetAppConfig("IISFrontGuard.Webhook.Url", "http://webhook1.com;;http://webhook2.com;  ;http://webhook3.com");

            // Act
            var urls = WebhookNotifier.GetWebhookUrls();

            // Assert
            Assert.AreEqual(3, urls.Length);
            Assert.AreEqual("http://webhook1.com", urls[0]);
            Assert.AreEqual("http://webhook2.com", urls[1]);
            Assert.AreEqual("http://webhook3.com", urls[2]);
        }

        [Test]
        public void GetWebhookUrls_WithEmptyString_ReturnsEmptyArray()
        {
            // Arrange
            SetAppConfig("IISFrontGuard.Webhook.Url", "");

            // Act
            var urls = WebhookNotifier.GetWebhookUrls();

            // Assert
            Assert.AreEqual(0, urls.Length);
        }

        [Test]
        public void GetWebhookUrls_WithNullConfig_ReturnsEmptyArray()
        {
            // Arrange
            ConfigurationManager.AppSettings["IISFrontGuard.Webhook.Url"] = null;

            // Act
            var urls = WebhookNotifier.GetWebhookUrls();

            // Assert
            Assert.AreEqual(0, urls.Length);
        }

        [Test]
        public async Task LogWebhookFailure_IsCalled_WhenAllRetriesFail()
        {
            // Arrange
            // Enable webhooks
            SetAppConfig("IISFrontGuard.Webhook.Enabled", "true");

            // Point webhook to the failing endpoint on the mock server
            SetAppConfig("IISFrontGuard.Webhook.Url", "http://localhost:8765/fail/");

            // Configure failure log path
            SetAppConfig("IISFrontGuard.Webhook.FailureLogPath", _testLogPath);

            // Create a security event and enqueue
            var securityEvent = new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "TEST_EVENT_FAILURE",
                Severity = "high",
                RayId = "ray-123",
                ClientIp = "127.0.0.1",
                CountryCode = "US",
                UserAgent = "UnitTest/1.0",
                HostName = "localhost",
                Url = "/fail/",
                HttpMethod = "POST",
                RuleId = 1,
                RuleName = "TestRule",
                Description = "Test failure",
                AdditionalData = null
            };

            // Act
            WebhookNotifier.EnqueueSecurityEvent(securityEvent, true);

            // Wait for processing and log file creation (with timeout)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Task.Delay(10000);

            // Assert            
            Assert.IsNotNull(sw);
            Assert.True(File.Exists(_testLogPath));
            Assert.IsTrue(new FileInfo(_testLogPath).Length > 0);
        }

        [Test]
        public async Task LogWebhookFailure_IsCalled_WhenAllRetriesFail_LogPathNull()
        {
            // Arrange
            // Enable webhooks
            SetAppConfig("IISFrontGuard.Webhook.Enabled", "true");

            // Point webhook to the failing endpoint on the mock server
            SetAppConfig("IISFrontGuard.Webhook.Url", "http://localhost:8765/fail/");

            // Configure failure log path
            SetAppConfig("IISFrontGuard.Webhook.FailureLogPath", null);

            // Create a security event and enqueue
            var securityEvent = new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "TEST_EVENT_FAILURE",
                Severity = "high",
                RayId = "ray-123",
                ClientIp = "127.0.0.1",
                CountryCode = "US",
                UserAgent = "UnitTest/1.0",
                HostName = "localhost",
                Url = "/fail/",
                HttpMethod = "POST",
                RuleId = 1,
                RuleName = "TestRule",
                Description = "Test failure",
                AdditionalData = null
            };

            // Act
            WebhookNotifier.EnqueueSecurityEvent(securityEvent, true);

            // Wait for processing and log file creation (with timeout)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Task.Delay(10000);

            // Assert            
            Assert.IsNotNull(sw);
            Assert.True(!File.Exists(_testLogPath));
        }

        [Test]
        public async Task LogWebhookFailure_IsCalled_WhenAllRetriesFail_LogFile_NotExist()
        {
            // Arrange
            // Enable webhooks
            SetAppConfig("IISFrontGuard.Webhook.Enabled", "true");

            // Point webhook to the failing endpoint on the mock server
            SetAppConfig("IISFrontGuard.Webhook.Url", "http://localhost:8765/fail/");

            // Configure failure log path
            string nonexistentPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.log");
            SetAppConfig("IISFrontGuard.Webhook.FailureLogPath", nonexistentPath);

            // Create a security event and enqueue
            var securityEvent = new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "TEST_EVENT_FAILURE",
                Severity = "high",
                RayId = "ray-123",
                ClientIp = "127.0.0.1",
                CountryCode = "US",
                UserAgent = "UnitTest/1.0",
                HostName = "localhost",
                Url = "/fail/",
                HttpMethod = "POST",
                RuleId = 1,
                RuleName = "TestRule",
                Description = "Test failure",
                AdditionalData = null
            };

            // Act
            WebhookNotifier.EnqueueSecurityEvent(securityEvent, true);

            // Wait for processing and log file creation (with timeout)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Task.Delay(15000);

            // Assert            
            Assert.IsNotNull(sw);
            Assert.True(File.Exists(nonexistentPath));
        }

        private static void SetAppConfig(string key, string value)
        {
            ConfigurationManager.AppSettings[key] = value;
        }

        private static void ResetWebhookNotifierState()
        {
            try
            {
                // Reset static fields via reflection
                var eventQueueField = typeof(WebhookNotifier).GetField("_eventQueue",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var eventCountsField = typeof(WebhookNotifier).GetField("_eventCounts",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var isRunningField = typeof(WebhookNotifier).GetField("_isRunning",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (eventQueueField != null)
                {
                    var queue = eventQueueField.GetValue(null) as System.Collections.Concurrent.ConcurrentQueue<SecurityEvent>;
                    while (queue != null && queue.TryDequeue(out _)) { }
                }

                if (eventCountsField != null)
                {
                    var counts = eventCountsField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, int>;
                    counts?.Clear();
                }

                // Ensure background processing is stopped between tests
                isRunningField?.SetValue(null, false);
            }
            catch
            {
                // Ignore reset errors
            }
        }

        private async Task HandleWebhookRequests()
        {
            while (_serverRunning && _mockWebhookServer != null && _mockWebhookServer.IsListening)
            {
                try
                {
                    var context = await _mockWebhookServer.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    _receivedCustomHeaders.Clear();
                    foreach (var key in request.Headers.AllKeys)
                    {
                        if (key.StartsWith("X-"))
                        {
                            _receivedCustomHeaders[key] = request.Headers[key];
                        }
                    }

                    // If the request path contains '/fail/', return an error to force retries
                    if (request.Url != null && request.Url.AbsolutePath.IndexOf("/fail/", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        response.StatusCode = 500;
                        response.Close();
                        continue;
                    }

                    // Return success
                    response.StatusCode = 200;
                    response.Close();
                }
                catch
                {
                    break;
                }
            }
        }
    }
}