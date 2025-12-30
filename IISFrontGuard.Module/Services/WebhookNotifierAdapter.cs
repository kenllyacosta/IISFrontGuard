using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Adapter that wraps the static WebhookNotifier class to provide instance-based dependency injection.
    /// </summary>
    public class WebhookNotifierAdapter : IWebhookNotifier
    {
        /// <summary>
        /// Enqueues a security event for asynchronous webhook notification.
        /// </summary>
        /// <param name="securityEvent">The security event to send.</param>
        public void EnqueueSecurityEvent(SecurityEvent securityEvent)
        {
            WebhookNotifier.EnqueueSecurityEvent(securityEvent);
        }

        /// <summary>
        /// Stops the background webhook notification service gracefully.
        /// </summary>
        public void Stop()
        {
            WebhookNotifier.Stop();
        }
    }
}
