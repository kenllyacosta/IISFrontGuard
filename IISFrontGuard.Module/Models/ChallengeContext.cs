using System;
using System.Web;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Encapsulates context information for challenge processing (managed or interactive).
    /// </summary>
    public class ChallengeContext
    {
        /// <summary>
        /// Gets or sets the HTTP request being processed.
        /// </summary>
        public HttpRequest Request { get; set; }

        /// <summary>
        /// Gets or sets the HTTP response to send to the client.
        /// </summary>
        public HttpResponse Response { get; set; }

        /// <summary>
        /// Gets or sets the clearance token from the client (if any).
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the encryption key for token operations.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the logging context for this request.
        /// </summary>
        public RequestLogContext LogContext { get; set; }

        /// <summary>
        /// Gets or sets the HTML generator function that creates the challenge page.
        /// The function takes (rootDomain, rayId, csrfToken) and returns the HTML string.
        /// </summary>
        public Func<string, string, string, string> HtmlGenerator { get; set; }
    }
}
