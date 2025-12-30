namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Defines constant values for different types of security events that can occur.
    /// </summary>
    public static class SecurityEventTypes
    {
        /// <summary>
        /// Indicates a SQL injection attack attempt was detected.
        /// </summary>
        public const string SQLInjectionAttempt = "sql_injection_attempt";

        /// <summary>
        /// Indicates a cross-site scripting (XSS) attack attempt was detected.
        /// </summary>
        public const string XSSAttempt = "xss_attempt";

        /// <summary>
        /// Indicates a path traversal attack attempt was detected.
        /// </summary>
        public const string PathTraversalAttempt = "path_traversal_attempt";

        /// <summary>
        /// Indicates a command injection attack attempt was detected.
        /// </summary>
        public const string CommandInjectionAttempt = "command_injection_attempt";

        /// <summary>
        /// Indicates a client has exceeded the configured rate limit.
        /// </summary>
        public const string RateLimitExceeded = "rate_limit_exceeded";

        /// <summary>
        /// Indicates a distributed attack pattern was detected.
        /// </summary>
        public const string DistributedAttack = "distributed_attack";

        /// <summary>
        /// Indicates a brute force attack attempt was detected.
        /// </summary>
        public const string BruteForceAttempt = "brute_force_attempt";

        /// <summary>
        /// Indicates a suspicious user agent was detected.
        /// </summary>
        public const string SuspiciousUserAgent = "suspicious_user_agent";

        /// <summary>
        /// Indicates an automated bot was detected.
        /// </summary>
        public const string BotDetected = "bot_detected";

        /// <summary>
        /// Indicates anomalous traffic patterns were detected.
        /// </summary>
        public const string AnomalousTraffic = "anomalous_traffic";

        /// <summary>
        /// Indicates a request was blocked by a WAF rule.
        /// </summary>
        public const string RequestBlocked = "request_blocked";

        /// <summary>
        /// Indicates a challenge was issued to verify the client.
        /// </summary>
        public const string ChallengeIssued = "challenge_issued";

        /// <summary>
        /// Indicates multiple challenge verification failures occurred.
        /// </summary>
        public const string MultipleChallengeFails = "multiple_challenge_fails";

        /// <summary>
        /// Indicates an invalid or expired token was presented.
        /// </summary>
        public const string InvalidToken = "invalid_token";

        /// <summary>
        /// Indicates a potential token replay attack was detected.
        /// </summary>
        public const string TokenReplayAttempt = "token_replay_attempt";

        /// <summary>
        /// Indicates a CSRF token validation failure.
        /// </summary>
        public const string CSRFTokenMismatch = "csrf_token_mismatch";

        /// <summary>
        /// Indicates an unexpected geographic location for the client.
        /// </summary>
        public const string UnexpectedGeoLocation = "unexpected_geo_location";

        /// <summary>
        /// Indicates a request from a high-risk country.
        /// </summary>
        public const string HighRiskCountry = "high_risk_country";
    }
}