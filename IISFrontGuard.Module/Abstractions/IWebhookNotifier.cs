using IISFrontGuard.Module.Models;

namespace IISFrontGuard.Module.Abstractions
{
    /// <summary>
    /// Defines an abstraction for sending security event notifications via webhook.
    /// </summary>
    public interface IWebhookNotifier
    {
        /// <summary>
        /// Enqueues a security event for asynchronous webhook notification.
        /// </summary>
        /// <param name="securityEvent">The security event to send.</param>
        void EnqueueSecurityEvent(SecurityEvent securityEvent);

        /// <summary>
        /// Stops the background webhook notification service gracefully.
        /// </summary>
        void Stop();
    }
}
