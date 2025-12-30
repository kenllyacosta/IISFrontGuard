using IISFrontGuard.Module.Models;
using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Provides webhook notification capabilities for security events with retry logic and throttling.
    /// </summary>
    public static class WebhookNotifier
    {
        private static readonly ConcurrentQueue<SecurityEvent> _eventQueue = new ConcurrentQueue<SecurityEvent>();
        private static readonly ConcurrentDictionary<string, int> _eventCounts = new ConcurrentDictionary<string, int>();
        private static readonly object _lockObject = new object();
        private static DateTime _lastResetTime = DateTime.UtcNow;
        private static volatile bool _isRunning = true;

        private const int MaxRetries = 3;
        private const int TimeoutSeconds = 10;
        private const int ThrottleWindowMinutes = 5;
        private const int MaxEventsPerWindow = 100;

        static WebhookNotifier()
        {
            Task.Factory.StartNew(ProcessWebhookQueueAsync, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Enqueues a security event for asynchronous webhook notification.
        /// </summary>
        /// <param name="securityEvent">The security event to send via webhook.</param>
        /// <param name="sendDirectly">Whether to send the event directly without queuing.</param>
        public static void EnqueueSecurityEvent(SecurityEvent securityEvent, bool sendDirectly = false)
        {
            if (securityEvent == null || !IsWebhookEnabled())
                return;

            // Throttle events to prevent webhook flooding
            if (ShouldThrottleEvent(securityEvent.EventType))
                return;

            if (!sendDirectly)
                _eventQueue.Enqueue(securityEvent);
            else
                Task.Run(() => SendWebhookNotificationAsync(securityEvent));
        }

        /// <summary>
        /// Checks if webhook notifications are enabled in configuration.
        /// </summary>
        /// <returns>True if webhooks are enabled; otherwise, false.</returns>
        private static bool IsWebhookEnabled()
        {
            var enabled = ConfigurationManager.AppSettings["IISFrontGuard.Webhook.Enabled"];
            return !string.IsNullOrEmpty(enabled) && enabled.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets webhook URLs from configuration (supports multiple webhooks separated by semicolon or comma).
        /// </summary>
        /// <returns>An array of webhook URLs.</returns>
        public static string[] GetWebhookUrls()
        {
            var webhookUrl = ConfigurationManager.AppSettings["IISFrontGuard.Webhook.Url"];
            if (string.IsNullOrEmpty(webhookUrl))
                return new string[0];

            return webhookUrl.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(url => url.Trim())
                             .Where(url => !string.IsNullOrEmpty(url))
                             .ToArray();
        }

        /// <summary>
        /// Implements event throttling to prevent webhook flooding.
        /// </summary>
        /// <param name="eventType">The event type to check.</param>
        /// <returns>True if the event should be throttled; otherwise, false.</returns>
        private static bool ShouldThrottleEvent(string eventType)
        {
            lock (_lockObject)
            {
                // Reset counter if window has expired
                if ((DateTime.UtcNow - _lastResetTime).TotalMinutes >= ThrottleWindowMinutes)
                {
                    _eventCounts.Clear();
                    _lastResetTime = DateTime.UtcNow;
                }

                var currentCount = _eventCounts.GetOrAdd(eventType, 0);
                if (currentCount >= MaxEventsPerWindow)
                    return true; // Throttle this event

                _eventCounts[eventType] = currentCount + 1;
                return false;
            }
        }

        /// <summary>
        /// Processes the webhook queue asynchronously in a background task.
        /// </summary>
        private static async Task ProcessWebhookQueueAsync()
        {
            while (_isRunning)
            {
                while (_eventQueue.TryDequeue(out var securityEvent))
                {
                    try
                    {
                        await SendWebhookNotificationAsync(securityEvent);
                    }
                    catch
                    {
                        // Silently fail - webhook errors shouldn't impact application
                    }
                }

                await Task.Delay(100); // Short pause to prevent CPU spinning
            }
        }

        /// <summary>
        /// Sends webhook notification with retry logic and exponential backoff.
        /// </summary>
        /// <param name="securityEvent">The security event to send.</param>
        private static async Task SendWebhookNotificationAsync(SecurityEvent securityEvent)
        {
            var webhookUrls = GetWebhookUrls();
            if (webhookUrls.Length == 0)
                return;

            var payload = BuildWebhookPayload(securityEvent);
            var jsonPayload = SerializeToJson(payload);

            foreach (var webhookUrl in webhookUrls)
            {
                for (int attempt = 0; attempt < MaxRetries; attempt++)
                {
                    try
                    {
                        var success = await SendHttpPostAsync(webhookUrl, jsonPayload);
                        if (success)
                            break; // Success, move to next webhook
                    }
                    catch
                    {
                        if (attempt == MaxRetries - 1)
                        {
                            // Final attempt failed, log to event log or file
                            LogWebhookFailure(webhookUrl, securityEvent.EventType);
                        }
                        else
                        {
                            // Wait before retry with exponential backoff
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the webhook payload with standardized format.
        /// </summary>
        /// <param name="securityEvent">The security event to format.</param>
        /// <returns>An anonymous object representing the webhook payload.</returns>
        private static object BuildWebhookPayload(SecurityEvent securityEvent)
        {
            return new
            {
                timestamp = securityEvent.Timestamp.ToString("o"), // ISO 8601 format
                event_type = securityEvent.EventType,
                severity = securityEvent.Severity,
                ray_id = securityEvent.RayId,
                source = new
                {
                    ip = securityEvent.ClientIp,
                    country_code = securityEvent.CountryCode,
                    user_agent = securityEvent.UserAgent
                },
                request = new
                {
                    hostname = securityEvent.HostName,
                    url = securityEvent.Url,
                    method = securityEvent.HttpMethod
                },
                waf_rule = new
                {
                    id = securityEvent.RuleId,
                    name = securityEvent.RuleName
                },
                description = securityEvent.Description,
                additional_data = securityEvent.AdditionalData,
                metadata = new
                {
                    application = "IISFrontGuard",
                    version = "1.0"
                }
            };
        }

        /// <summary>
        /// Serializes an object to JSON string.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A JSON string representation of the object.</returns>
        private static string SerializeToJson(object obj)
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(obj);
        }

        /// <summary>
        /// Sends an HTTP POST request to a webhook URL.
        /// </summary>
        /// <param name="webhookUrl">The webhook URL to send to.</param>
        /// <param name="jsonPayload">The JSON payload to send.</param>
        /// <returns>True if the webhook was delivered successfully; otherwise, false.</returns>
        private static async Task<bool> SendHttpPostAsync(string webhookUrl, string jsonPayload)
        {
            var request = (HttpWebRequest)WebRequest.Create(webhookUrl);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = TimeoutSeconds * 1000;
            request.UserAgent = "IISFrontGuard-Webhook/1.0";

            ConfigureRequestHeaders(request);

            // Write payload
            using (var streamWriter = new StreamWriter(await request.GetRequestStreamAsync()))
            {
                await streamWriter.WriteAsync(jsonPayload);
                await streamWriter.FlushAsync();
            }

            // Get response
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                return IsSuccessStatusCode(response.StatusCode);
            }
        }

        /// <summary>
        /// Configures authentication and custom headers for the webhook request.
        /// </summary>
        /// <param name="request">The HTTP request to configure.</param>
        private static void ConfigureRequestHeaders(HttpWebRequest request)
        {
            var authHeader = ConfigurationManager.AppSettings["IISFrontGuard.Webhook.AuthHeader"];
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Add("Authorization", authHeader);

            AddCustomHeaders(request);
        }

        /// <summary>
        /// Adds custom headers from configuration to the request.
        /// </summary>
        /// <param name="request">The HTTP request to add headers to.</param>
        private static void AddCustomHeaders(HttpWebRequest request)
        {
            var customHeaders = ConfigurationManager.AppSettings["IISFrontGuard.Webhook.CustomHeaders"];
            if (string.IsNullOrWhiteSpace(customHeaders))
                return;

            var headerEntries = customHeaders.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var header in headerEntries)
            {
                var trimmedHeader = header.Trim();
                var parts = trimmedHeader.Split(new[] { ':' }, 2);
                if (parts.Length == 2)
                {
                    var headerName = parts[0].Trim();
                    var headerValue = parts[1].Trim();
                    if (!string.IsNullOrEmpty(headerName) && !string.IsNullOrEmpty(headerValue))
                    {
                        request.Headers.Add(headerName, headerValue);
                    }
                }
            }
        }

        /// <summary>
        /// Determines if an HTTP status code indicates success.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to check.</param>
        /// <returns>True if the status code indicates success; otherwise, false.</returns>
        private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.OK ||
                   statusCode == HttpStatusCode.Accepted ||
                   statusCode == HttpStatusCode.Created ||
                   statusCode == HttpStatusCode.NoContent;
        }

        /// <summary>
        /// Logs webhook delivery failures to a file.
        /// </summary>
        /// <param name="webhookUrl">The webhook URL that failed.</param>
        /// <param name="eventType">The event type that failed to deliver.</param>
        private static void LogWebhookFailure(string webhookUrl, string eventType)
        {
            var logPath = ConfigurationManager.AppSettings["IISFrontGuard.Webhook.FailureLogPath"];
            if (string.IsNullOrWhiteSpace(logPath))
                return;

            if (!File.Exists(logPath))
                using (File.Create(logPath)) { /*Free file handle*/ }

            var logEntry = $"{DateTime.UtcNow:o} - Failed to deliver {eventType} to {webhookUrl}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry);
        }

        /// <summary>
        /// Stops the webhook notifier gracefully, waiting for pending events to be processed.
        /// </summary>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait for pending events.</param>
        public static void Stop(int maxWaitSeconds = 5)
        {
            _isRunning = false;

            // Wait for pending events to be processed (with timeout)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (!_eventQueue.IsEmpty && stopwatch.Elapsed.TotalSeconds < maxWaitSeconds)
                System.Threading.Thread.Sleep(100);
        }
    }
}
