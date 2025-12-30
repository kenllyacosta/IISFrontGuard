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
        /// Gets or sets the logic operator for combining with other conditions (1=AND, 2=OR).
        /// </summary>
        public byte? LogicOperator { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the WAF rule this condition belongs to.
        /// </summary>
        public int WafRuleEntityId { get; set; }

        /// <summary>
        /// Gets or sets the name of the field to evaluate (used for cookies and headers).
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// Gets or sets the order in which conditions are evaluated.
        /// </summary>
        public int ConditionOrder { get; set; }

        /// <summary>
        /// Gets or sets the creation date of the condition.
        /// </summary>
        public DateTime CreationDate { get; set; }
    }
}