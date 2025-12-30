namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Defines constant values for security event severity levels.
    /// </summary>
    public static class SecurityEventSeverity
    {
        /// <summary>
        /// Indicates a critical severity event requiring immediate attention.
        /// </summary>
        public const string Critical = "critical";

        /// <summary>
        /// Indicates a high severity event requiring prompt attention.
        /// </summary>
        public const string High = "high";

        /// <summary>
        /// Indicates a medium severity event.
        /// </summary>
        public const string Medium = "medium";

        /// <summary>
        /// Indicates a low severity event.
        /// </summary>
        public const string Low = "low";

        /// <summary>
        /// Indicates an informational event with no immediate security concern.
        /// </summary>
        public const string Info = "info";
    }
}