using System;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Tracks rate limiting information for a client IP address.
    /// </summary>
    public class RateLimitInfo
    {
        /// <summary>
        /// Gets or sets the number of requests made within the current time window.
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the start time of the current rate limit window.
        /// </summary>
        public DateTime WindowStart { get; set; }
    }
}