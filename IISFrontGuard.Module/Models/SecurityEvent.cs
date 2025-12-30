using System;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Represents a security event that can be sent via webhook notification.
    /// </summary>
    public class SecurityEvent
    {
        /// <summary>
        /// Gets or sets the type of security event (e.g., RequestBlocked, ChallengeIssued, RateLimitExceeded).
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the event (Critical, High, Medium, Low).
        /// </summary>
        public string Severity { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the unique Ray ID for tracking the request.
        /// </summary>
        public string RayId { get; set; }

        /// <summary>
        /// Gets or sets the client IP address that triggered the event.
        /// </summary>
        public string ClientIp { get; set; }

        /// <summary>
        /// Gets or sets the hostname of the request.
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Gets or sets the user agent string of the client.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the full URL of the request.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method of the request (GET, POST, etc.).
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the WAF rule that triggered the event (if applicable).
        /// </summary>
        public int? RuleId { get; set; }

        /// <summary>
        /// Gets or sets the name of the WAF rule that triggered the event (if applicable).
        /// </summary>
        public string RuleName { get; set; }

        /// <summary>
        /// Gets or sets the ISO country code of the client.
        /// </summary>
        public string CountryCode { get; set; }

        /// <summary>
        /// Gets or sets a human-readable description of the event.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets additional event-specific data.
        /// </summary>
        public object AdditionalData { get; set; }
    }
}