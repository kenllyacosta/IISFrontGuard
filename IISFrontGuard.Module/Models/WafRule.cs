using System;
using System.Collections.Generic;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Represents a Web Application Firewall (WAF) rule that defines security policies.
    /// </summary>
    public class WafRule
    {
        /// <summary>
        /// Gets or sets the unique identifier for the WAF rule.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the WAF rule.
        /// </summary>
        public string Nombre { get; set; }

        /// <summary>
        /// Gets or sets the action identifier to take when the rule matches (1=skip, 2=block, 3=managed challenge, 4=interactive challenge, 5=log).
        /// </summary>
        public byte ActionId { get; set; }

        /// <summary>
        /// Gets or sets the application identifier that owns this rule.
        /// </summary>
        public Guid AppId { get; set; }

        /// <summary>
        /// Gets or sets the priority of the rule. Lower numbers have higher priority.
        /// </summary>
        public int? Prioridad { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the rule is enabled.
        /// </summary>
        public bool Habilitado { get; set; }

        /// <summary>
        /// Gets or sets the creation date of the rule.
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the list of conditions that must be met for this rule to trigger.
        /// </summary>
        public List<WafCondition> Conditions { get; set; }
    }
}