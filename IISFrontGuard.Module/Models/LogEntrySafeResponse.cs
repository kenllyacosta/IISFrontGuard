using System;
using System.Web;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Represents safely sanitized HTTP response data for logging purposes.
    /// </summary>
    public class LogEntrySafeResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier for this log entry.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the URL of the request.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method (GET, POST, etc.).
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// Gets or sets the response time in milliseconds.
        /// </summary>
        public long ResponseTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the response was logged.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the Ray ID for correlating request and response.
        /// </summary>
        public Guid? RayId { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code of the response.
        /// </summary>
        public short? StatusCode { get; set; }

        /// <summary>
        /// Creates a LogEntrySafeResponse instance from an HttpResponse and HttpRequest.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="rayId">The Ray ID for correlation.</param>
        /// <param name="responseTime">The response time in milliseconds.</param>
        /// <returns>A new LogEntrySafeResponse instance.</returns>
        public static LogEntrySafeResponse FromHttpResponse(HttpResponse response, HttpRequest request, Guid? rayId, long responseTime)
            => new LogEntrySafeResponse
            {
                Id = Guid.NewGuid(),
                Url = SafeUrlEncode(request.Url?.ToString()),
                HttpMethod = request.HttpMethod,
                ResponseTime = responseTime,
                Timestamp = DateTime.UtcNow,
                RayId = rayId,
                StatusCode = (short?)response.StatusCode
            };

        /// <summary>
        /// Safely URL encodes a string, handling null values.
        /// </summary>
        /// <param name="value">The string to encode.</param>
        /// <returns>The URL-encoded string, or empty string if the input is null or empty.</returns>
        public static string SafeUrlEncode(string value)
            => string.IsNullOrEmpty(value) ? string.Empty : HttpUtility.UrlEncode(value);
    }
}