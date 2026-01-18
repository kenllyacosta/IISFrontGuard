using System.Collections.Generic;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Represents a group of WAF conditions that must be evaluated together.
    /// </summary>
    public sealed class WafGroup
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the list of WAF conditions in this group.
        /// </summary>
        public List<WafCondition> Conditions { get; set; } = new List<WafCondition>();
    }
}