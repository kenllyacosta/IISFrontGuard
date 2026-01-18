using System;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Represents a pre-compiled WAF rule with optimized evaluation delegates.
    /// Rules are compiled once and cached for fast repeated evaluation.
    /// </summary>
    public class CompiledRule
    {
        /// <summary>
        /// Gets or sets the rule identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the rule name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the action ID (1=skip, 2=block, 3=managed challenge, etc.).
        /// </summary>
        public byte ActionId { get; set; }

        /// <summary>
        /// Gets or sets the rule priority (lower = higher priority).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the application ID.
        /// </summary>
        public Guid AppId { get; set; }

        /// <summary>
        /// Gets or sets the compiled evaluation function.
        /// This delegate evaluates the entire rule (all groups + conditions) in one call.
        /// Returns true if the rule matches the request context.
        /// </summary>
        public Func<RequestContext, bool> Evaluate { get; set; }

        /// <summary>
        /// Gets or sets the original uncompiled rule for metadata access.
        /// </summary>
        public WafRule OriginalRule { get; set; }

        /// <summary>
        /// Gets whether this rule uses the optimized compiled path.
        /// </summary>
        public bool IsCompiled => Evaluate != null;
    }
}
