using System;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Represents a condition within a WAF rule that must be evaluated against HTTP requests.
    /// </summary>
    public class WafCondition
    {
        /// <summary>
        /// Gets or sets the unique identifier for the condition.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the field identifier to evaluate (e.g., 1=cookie, 2=hostname, 3=ip, etc.).
        /// </summary>
        public byte FieldId { get; set; }

        /// <summary>
        /// Gets or sets the operator identifier for comparison (e.g., 1=equals, 3=contains, 5=regex, etc.).
        /// </summary>
        public byte OperatorId { get; set; }

        /// <summary>
        /// Gets or sets the value to compare against.
        /// </summary>
        public string Valor { get; set; }

        /// <summary>
        /// Gets or sets the name of the field to evaluate (used for cookies and headers).
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to negate the condition's result.
        /// </summary>
        public bool Negate { get; set; }

        /// <summary>
        /// Gets or sets the creation date of the condition.
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the WAF group this condition belongs to (new group-based model).
        /// </summary>
        public int? WafGroupId { get; set; }

        // Legacy properties (for backward compatibility with flat condition lists)

        /// <summary>
        /// Gets or sets the identifier of the WAF rule this condition belongs to.
        /// </summary>
        [Obsolete("Use WafGroupId instead. This is for backward compatibility only.")]
        public int WafRuleEntityId { get; set; }

        /// <summary>
        /// Gets or sets the logic operator for combining with other conditions (1=AND, 2=OR).
        /// </summary>
        [Obsolete("Logic is now determined by group membership. This is for backward compatibility only.")]
        public byte? LogicOperator { get; set; }

        /// <summary>
        /// Gets or sets the order in which conditions are evaluated.
        /// </summary>
        [Obsolete("Order is now determined by group membership. This is for backward compatibility only.")]
        public int ConditionOrder { get; set; }
    }
}