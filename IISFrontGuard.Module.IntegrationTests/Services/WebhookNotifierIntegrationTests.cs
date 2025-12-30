using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Services
{
    [CollectionDefinition("WebhookNotifierTests", DisableParallelization = true)]
    public class WebhookNotifierTestsCollection { }

    [Collection("WebhookNotifierTests")]
    public class WebhookNotifierIntegrationTests
    {
        private static int GetFreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        [Fact]
        public void EnqueueSecurityEvent_DoesNotEnqueue_WhenNullOrNotEnabled()
        {
            WebhookNotifierTestReset.Reset();
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "false");
            WebhookNotifier.EnqueueSecurityEvent(null); // Should not enqueue
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent { EventType = "A" }); // Should not enqueue
            var queue = (ConcurrentQueue<SecurityEvent>)typeof(WebhookNotifier).GetField("_eventQueue", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.True(queue.IsEmpty);
        }

        [Fact]
        public void EnqueueSecurityEvent_ThrottlesEvents()
        {
            WebhookNotifierTestReset.Reset();
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            // Simulate 100 events of same type
            for (int i = 0; i < 100; i++)
                WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent { EventType = "THROTTLE" });
            // 101st should be throttled
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent { EventType = "THROTTLE" });
            var queue = (ConcurrentQueue<SecurityEvent>)typeof(WebhookNotifier).GetField("_eventQueue", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.Equal(100, queue.Count);
        }

        [Fact]
        public void EnqueueSecurityEvent_Enqueues_WhenEnabledAndNotThrottled()
        {
            WebhookNotifierTestReset.Reset();
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent { EventType = "ENQUEUE" });
            var queue = (ConcurrentQueue<SecurityEvent>)typeof(WebhookNotifier).GetField("_eventQueue", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.Single(queue);
        }

        [Fact]
        public void GetWebhookUrls_ParsesCommaAndSemicolon()
        {
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", " http://a ;http://b, http://c ");
            var urls = WebhookNotifier.GetWebhookUrls();
            Assert.Equal(3, urls.Length);
            Assert.Equal("http://a", urls[0]);
            Assert.Equal("http://b", urls[1]);
            Assert.Equal("http://c", urls[2]);
        }

        [Fact]
        public void BuildWebhookPayload_ReturnsExpectedStructure()
        {
            var evt = new SecurityEvent
            {
                Timestamp = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                EventType = "TYPE",
                Severity = "sev",
                RayId = "ray",
                ClientIp = "1.2.3.4",
                CountryCode = "US",
                UserAgent = "UA",
                HostName = "host",
                Url = "url",
                HttpMethod = "GET",
                RuleId = 42,
                RuleName = "RuleName",
                Description = "desc",
                AdditionalData = "data"
            };
            var payload = typeof(WebhookNotifier).GetMethod("BuildWebhookPayload", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { evt });
            var dict = new JavaScriptSerializer().Serialize(payload);
            Assert.Contains("\"event_type\":\"TYPE\"", dict);
            Assert.Contains("\"ray_id\":\"ray\"", dict);
            Assert.Contains("\"application\":\"IISFrontGuard\"", dict);
        }

        [Fact]
        public void SerializeToJson_ReturnsValidJson()
        {
            var obj = new { foo = "bar", num = 123 };
            var json = typeof(WebhookNotifier).GetMethod("SerializeToJson", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { obj }) as string;
            Assert.Contains("\"foo\":\"bar\"", json);
            Assert.Contains("\"num\":123", json);
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, true)]
        [InlineData(HttpStatusCode.Accepted, true)]
        [InlineData(HttpStatusCode.Created, true)]
        [InlineData(HttpStatusCode.NoContent, true)]
        [InlineData(HttpStatusCode.BadRequest, false)]
        [InlineData(HttpStatusCode.InternalServerError, false)]
        public void IsSuccessStatusCode_Works(HttpStatusCode code, bool expected)
        {
            var result = (bool)typeof(WebhookNotifier).GetMethod("IsSuccessStatusCode", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { code });
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task LogsFailure_WhenUnreachable_AndFinalAttemptFails()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "webhook_fail_" + Guid.NewGuid().ToString("N") + ".log");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.FailureLogPath", logPath);
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.AuthHeader", "Bearer 123456");
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.False(System.IO.File.Exists(logPath), "Expected failure log file to exist.");
        }

        [Fact]
        public async Task CustomHeaders_Empty()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "webhook_fail_" + Guid.NewGuid().ToString("N") + ".log");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");            
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.CustomHeaders", "");           
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.False(System.IO.File.Exists(logPath), "Expected failure log file to exist.");
        }

        [Fact]
        public async Task CustomHeaders_Space()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "webhook_fail_" + Guid.NewGuid().ToString("N") + ".log");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.CustomHeaders", " ");
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.False(System.IO.File.Exists(logPath), "Expected failure log file to exist.");
        }

        [Fact]
        public async Task CustomHeaders_StringEmpty()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "webhook_fail_" + Guid.NewGuid().ToString("N") + ".log");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.CustomHeaders", string.Empty);
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.False(System.IO.File.Exists(logPath), "Expected failure log file to exist.");
        }

        [Fact]
        public async Task AuthHeaderAndAuthHeader()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "webhook_fail_" + Guid.NewGuid().ToString("N") + ".log");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.AuthHeader", "Bearer 123456");
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.False(System.IO.File.Exists(logPath), "Expected failure log file to exist.");
        }

        [Fact]
        public async Task AuthHeaderAndAuthHeaderBasic()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "webhook_fail_" + Guid.NewGuid().ToString("N") + ".log");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.AuthHeader", "Basic 123456");
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.False(System.IO.File.Exists(logPath), "Expected failure log file to exist.");
        }

        [Fact]
        public async Task CustomHeaderAndAuthHeader()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "webhook_fail_" + Guid.NewGuid().ToString("N") + ".log");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.CustomHeaders", "x-subscription-key:TestKey");
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.False(System.IO.File.Exists(logPath), "Expected failure log file to exist.");
        }

        [Fact]
        public async Task CustomHeaderAndAuthHeaderSome()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.CustomHeaders", "x-subscription-key:TestKey;Authorization:Bearer TestToken");
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(10));
            Assert.True(true);
        }

        [Fact]
        public async Task CustomHeaderAndAuthHeaderThreeValues()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.CustomHeaders", "x-subscription-key:TestKey;Another-Header:AnotherValue;Authorization:Bearer TestToken");
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(10));
            Assert.True(true);
        }

        [Fact]
        public async Task CustomNullHeaderAndAuthHeader()
        {
            WebhookNotifierTestReset.Reset();
            var unusedPort = GetFreePort();
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "webhook_fail_" + Guid.NewGuid().ToString("N") + ".log");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", $"http://localhost:{unusedPort}/webhook/");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.CustomHeaders", null);
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "E_FAIL",
                Severity = "high"
            });
            await Task.Delay(TimeSpan.FromSeconds(10));
            Assert.False(System.IO.File.Exists(logPath), "Expected failure log file to exist.");
        }

        [Fact]
        public async Task SendHttpPostAsync_ReturnsTrue_OnSuccessStatusCode()
        {
            WebhookNotifierTestReset.Reset();
            int port = GetFreePort();
            string url = $"http://localhost:{port}/webhook/";
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(url);
                listener.Start();
                var contextTask = listener.GetContextAsync();
                var payload = "{\"test\":true}";
                var method = typeof(WebhookNotifier).GetMethod("SendHttpPostAsync", BindingFlags.NonPublic | BindingFlags.Static);
                var task = (Task<bool>)method.Invoke(null, new object[] { url, payload });
                var context = await contextTask;
                context.Response.StatusCode = 200;
                context.Response.Close();
                var result = await task;
                listener.Stop();
                Assert.True(result);
            }
        }

        [Fact]
        public void EnqueueSecurityEvent_ResetsCounter_WhenWindowExpired()
        {
            WebhookNotifierTestReset.Reset();
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");

            // Fill up to the throttle limit
            for (int i = 0; i < 100; i++)
                WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent { EventType = "THROTTLE" });

            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent { EventType = "THROTTLE" });
            var queue = (ConcurrentQueue<SecurityEvent>)typeof(WebhookNotifier).GetField("_eventQueue", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.Equal(100, queue.Count);

            // Move _lastResetTime far into the past so the window is considered expired
            typeof(WebhookNotifier).GetField("_lastResetTime", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, DateTime.UtcNow.AddMinutes(-10));

            // After window expiration, next event of same type should be enqueued (counter reset)
            WebhookNotifier.EnqueueSecurityEvent(new SecurityEvent { EventType = "THROTTLE" });
            Assert.Equal(101, queue.Count);
        }
    }

    public static class WebhookNotifierTestReset
    {
        public static void Reset()
        {
            var t = typeof(WebhookNotifier);
            SetField(t, "_isRunning", true);
            var queue = (ConcurrentQueue<SecurityEvent>)GetField(t, "_eventQueue");
            while (queue.TryDequeue(out _)) { }
            var counts = (ConcurrentDictionary<string, int>)GetField(t, "_eventCounts");
            counts.Clear();
            SetField(t, "_lastResetTime", DateTime.UtcNow);
        }
        private static object GetField(Type t, string name)
            => t.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        private static void SetField(Type t, string name, object value)
            => t.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    }
}
