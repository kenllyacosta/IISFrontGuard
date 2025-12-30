namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Encapsulates context information for request logging operations.
    /// </summary>
    public class RequestLogContext
    {
        /// <summary>
        /// Gets or sets the identifier of the WAF rule that was triggered (if any).
        /// </summary>
        public int? RuleTriggered { get; set; }

        /// <summary>
        /// Gets or sets the database connection string for logging.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the unique Ray ID for tracking this request.
        /// </summary>
        public string RayId { get; set; }

        /// <summary>
        /// Gets or sets the two-letter ISO country code of the client.
        /// </summary>
        public string Iso2 { get; set; }

        /// <summary>
        /// Gets or sets the action identifier that was taken (if any).
        /// </summary>
        public int? ActionId { get; set; }

        /// <summary>
        /// Gets or sets the application identifier.
        /// </summary>
        public string AppId { get; set; }
    }
}
