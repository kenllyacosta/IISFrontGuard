using System;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Tracks challenge failure information per IP address to detect brute force attempts.
    /// </summary>
    public class ChallengeFailureInfo
    {
        /// <summary>
        /// Gets or sets the timestamp of the first challenge failure in the current tracking window.
        /// </summary>
        public DateTime FirstFailure { get; set; }

        /// <summary>
        /// Gets or sets the number of consecutive challenge failures.
        /// </summary>
        public int FailureCount { get; set; }
    }
}