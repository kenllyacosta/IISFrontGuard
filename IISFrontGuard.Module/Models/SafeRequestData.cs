using System;
using System.Web;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Represents safely sanitized HTTP request data for logging purposes.
    /// </summary>
    public class SafeRequestData
    {
        /// <summary>
        /// Gets or sets the unique Ray ID for tracking this request.
        /// </summary>
        public string RayId { get; set; }

        /// <summary>
        /// Gets or sets the hostname of the request.
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Gets or sets the application identifier.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// Gets or sets the client IP address.
        /// </summary>
        public string IPAddress { get; set; }

        /// <summary>
        /// Gets or sets the protocol (http or https).
        /// </summary>
        public string Protocol { get; set; }

        /// <summary>
        /// Gets or sets the referrer URL.
        /// </summary>
        public string Referrer { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method (GET, POST, etc.).
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// Gets or sets the HTTP version.
        /// </summary>
        public string HttpVersion { get; set; }

        /// <summary>
        /// Gets or sets the user agent string.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the X-Forwarded-For header value.
        /// </summary>
        public string XForwardedFor { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the request.
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Gets or sets the full URL of the request.
        /// </summary>
        public string UrlFull { get; set; }

        /// <summary>
        /// Gets or sets the URL path without query string.
        /// </summary>
        public string UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the URL path with query string.
        /// </summary>
        public string UrlPathAndQuery { get; set; }

        /// <summary>
        /// Gets or sets the query string portion of the URL.
        /// </summary>
        public string UrlQueryString { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the WAF rule that was triggered (if any).
        /// </summary>
        public int? RuleId { get; set; }

        /// <summary>
        /// Gets or sets the action identifier that was taken (if any).
        /// </summary>
        public int? ActionId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this request data was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the request body content.
        /// </summary>
        public string RequestBody { get; set; }

        /// <summary>
        /// Gets or sets the two-letter ISO country code of the client.
        /// </summary>
        public string CountryIso2 { get; set; }

        /// <summary>
        /// Creates a SafeRequestData instance from an HttpRequest, sanitizing all values.
        /// </summary>
        /// <param name="req">The HTTP request to extract data from.</param>
        /// <param name="ruleTriggered">The WAF rule that was triggered (if any).</param>
        /// <param name="rayId">The unique Ray ID for this request.</param>
        /// <param name="iso2">The two-letter ISO country code.</param>
        /// <param name="actionId">The action that was taken (if any).</param>
        /// <param name="appId">The application identifier.</param>
        /// <param name="requestBody">The request body content.</param>
        /// <returns>A new SafeRequestData instance with all values safely encoded.</returns>
        public static SafeRequestData FromHttpRequest(HttpRequest req, int? ruleTriggered, string rayId, string iso2, int? actionId, string appId, string requestBody)
            => new SafeRequestData
            {
                RayId = rayId,
                HostName = req.Url?.Host,
                IPAddress = req.UserHostAddress,
                Protocol = req.Url?.Scheme,
                Referrer = SafeUrlEncode(req.UrlReferrer?.ToString()),
                HttpMethod = req.HttpMethod,
                HttpVersion = req.ServerVariables["HTTP_VERSION"],
                UserAgent = SafeUrlEncode(req.UserAgent),
                XForwardedFor = req.Headers["X-Forwarded-For"],
                MimeType = SafeUrlEncode(req.ContentType),
                UrlFull = SafeUrlEncode(req.Url?.ToString()),
                UrlPath = SafeUrlEncode(req.Url?.AbsolutePath),
                UrlPathAndQuery = SafeUrlEncode(req.Url?.PathAndQuery),
                UrlQueryString = SafeUrlEncode(req.Url?.Query),
                RuleId = ruleTriggered,
                CreatedAt = DateTime.UtcNow,
                CountryIso2 = iso2, 
                ActionId = actionId,
                AppId = appId, 
                RequestBody = requestBody
            };

        /// <summary>
        /// Safely URL encodes a string, handling null values.
        /// </summary>
        /// <param name="value">The string to encode.</param>
        /// <returns>The URL-encoded string, or empty string if the input is null or empty.</returns>
        private static string SafeUrlEncode(string value)
            => string.IsNullOrEmpty(value) ? string.Empty : HttpUtility.UrlEncode(value);
    }
}