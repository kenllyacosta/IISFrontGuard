using MaxMind.GeoIP2.Responses;
using System;
using System.Collections.Generic;
using System.Web;

namespace IISFrontGuard.Module.Models
{
    /// <summary>
    /// Optimized request context that caches all extracted values to avoid repeated parsing.
    /// Values are extracted once per request and reused across all rule evaluations.
    /// </summary>
    public class RequestContext
    {
        /// <summary>
        /// Gets or sets the client IP address. Considers proxy headers like CF-Connecting-IP and X-Forwarded-For.
        /// </summary>
        public string ClientIp { get; set; }

        /// <summary>
        /// Gets or sets the User-Agent string from the HTTP request.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the absolute path of the request URL without query string (e.g., "/api/users").
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the path and query string of the request URL (e.g., "/api/users?id=123").
        /// </summary>
        public string PathAndQuery { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method of the request (e.g., GET, POST, PUT, DELETE).
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the hostname from the request URL (e.g., "example.com").
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the protocol of the request (either "http" or "https").
        /// </summary>
        public string Protocol { get; set; }

        /// <summary>
        /// Gets or sets the query string from the request URL (e.g., "?id=123%26name=test").
        /// </summary>
        public string QueryString { get; set; }

        /// <summary>
        /// Gets or sets the complete absolute URI of the request (e.g., "https://example.com/api/users?id=123").
        /// </summary>
        public string FullUrl { get; set; }

        /// <summary>
        /// Gets or sets the referrer URL (the page that linked to this request).
        /// </summary>
        public string Referrer { get; set; }

        /// <summary>
        /// Gets or sets the Content-Type header value from the request.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the two-letter ISO country code from GeoIP lookup (e.g., "US", "GB", "CA").
        /// </summary>
        public string CountryIso2 { get; set; }

        /// <summary>
        /// Gets or sets the country name from GeoIP lookup (e.g., "United States", "United Kingdom").
        /// </summary>
        public string CountryName { get; set; }

        /// <summary>
        /// Gets or sets the continent name from GeoIP lookup (e.g., "North America", "Europe").
        /// </summary>
        public string ContinentName { get; set; }

        /// <summary>
        /// Gets or sets the HTTP version from the request (e.g., "HTTP/1.1", "HTTP/2").
        /// </summary>
        public string HttpVersion { get; set; }

        /// <summary>
        /// Gets or sets the X-Forwarded-For header value, containing proxy chain information.
        /// </summary>
        public string XForwardedFor { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the request, extracted from Content-Type header or file extension.
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Gets or sets the length of the request body in bytes.
        /// </summary>
        public long BodyLength { get; set; }

        // Lazy-loaded collections (only populated on first access)
        private Dictionary<string, string> _cookies;
        private Dictionary<string, string> _headers;
        private string _body;

        // Original request for lazy loading
        private readonly HttpRequest _request;
        private readonly Func<HttpRequest, string> _bodyExtractor;

        /// <summary>
        /// Initializes a new request context from an HTTP request.
        /// Eagerly extracts common values to avoid repeated parsing.
        /// </summary>
        public RequestContext(HttpRequest request, CountryResponse geoInfo, Func<HttpRequest, string> bodyExtractor)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _bodyExtractor = bodyExtractor;

            // Extract common values once
            ClientIp = GetClientIpInternal(request);
            UserAgent = request.UserAgent ?? string.Empty;
            Path = request.Url?.AbsolutePath ?? string.Empty;
            PathAndQuery = request.Url?.PathAndQuery ?? string.Empty;
            Method = request.HttpMethod ?? string.Empty;
            Host = request.Url?.Host ?? string.Empty;
            Protocol = request.IsSecureConnection ? "https" : "http";
            QueryString = request.Url?.Query ?? string.Empty;
            FullUrl = request.Url?.AbsoluteUri ?? string.Empty;
            Referrer = request.UrlReferrer?.AbsoluteUri ?? string.Empty;
            ContentType = request.ContentType ?? string.Empty;
            HttpVersion = request.ServerVariables["SERVER_PROTOCOL"] ?? string.Empty;
            XForwardedFor = request.Headers["X-Forwarded-For"] ?? string.Empty;
            BodyLength = request.InputStream?.Length ?? 0;

            // Extract GeoIP values
            CountryIso2 = geoInfo?.Country?.IsoCode ?? string.Empty;
            CountryName = geoInfo?.Country?.Name ?? string.Empty;
            ContinentName = geoInfo?.Continent?.Name ?? string.Empty;

            // Extract MIME type
            MimeType = GetMimeTypeInternal(request);
        }

        /// <summary>
        /// Gets a cookie value by name. Values are cached after first access.
        /// </summary>
        public string GetCookie(string name)
        {
            if (_cookies == null)
            {
                _cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (_request.Cookies != null)
                {
                    for (int i = 0; i < _request.Cookies.Count; i++)
                    {
                        var cookie = _request.Cookies[i];
                        if (cookie != null && !string.IsNullOrEmpty(cookie.Name))
                            _cookies[cookie.Name] = cookie.Value ?? string.Empty;
                    }
                }
            }

            return _cookies.TryGetValue(name, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Gets a header value by name. Values are cached after first access.
        /// </summary>
        public string GetHeader(string name)
        {
            if (_headers == null)
            {
                _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (_request.Headers != null)
                {
                    foreach (string key in _request.Headers.Keys)
                    {
                        if (!string.IsNullOrEmpty(key))
                        {
                            _headers[key] = _request.Headers[key] ?? string.Empty;
                        }
                    }
                }
            }

            return _headers.TryGetValue(name, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Gets the request body. Extracted only once on first access.
        /// </summary>
        public string GetBody()
        {
            if (_body == null && _bodyExtractor != null)
            {
                _body = _bodyExtractor(_request) ?? string.Empty;
            }
            return _body ?? string.Empty;
        }

        /// <summary>
        /// Gets client IP from Cloudflare or proxy headers.
        /// </summary>
        public string GetClientIpFromHeader(string headerName)
        {
            var value = GetHeader(headerName);
            return !string.IsNullOrEmpty(value) ? value : ClientIp;
        }

        private static string GetClientIpInternal(HttpRequest request)
        {
            // Simple extraction - trust proxy headers if configured
            var directIp = request.UserHostAddress ?? string.Empty;
            
            // Check if behind Cloudflare (most common case)
            var cfIp = request.Headers["CF-Connecting-IP"];
            if (!string.IsNullOrEmpty(cfIp))
                return cfIp;

            // Check X-Forwarded-For
            var xForwardedFor = request.Headers["X-Forwarded-For"];
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                // Take first IP (original client)
                var firstIp = xForwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(firstIp))
                    return firstIp;
            }

            return directIp;
        }

        private static string GetMimeTypeInternal(HttpRequest request)
        {
            var contentType = request.ContentType;
            if (!string.IsNullOrEmpty(contentType))
            {
                var semicolonIndex = contentType.IndexOf(';');
                return semicolonIndex > 0
                    ? contentType.Substring(0, semicolonIndex).Trim().ToLower()
                    : contentType.Trim().ToLower();
            }

            var path = request.Url?.AbsolutePath;
            if (!string.IsNullOrEmpty(path))
            {
                var extension = System.IO.Path.GetExtension(path);
                if (!string.IsNullOrEmpty(extension))
                {
                    return MimeMapping.GetMimeMapping(path).ToLower();
                }
            }

            return string.Empty;
        }
    }
}
