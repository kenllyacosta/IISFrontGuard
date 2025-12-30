using System;
using System.Web;

namespace IISFrontGuard.Module.Abstractions
{
    /// <summary>
    /// Abstraction for managing HTTP response headers, testable without IIS.
    /// </summary>
    public interface IResponseHeaderManager
    {
        /// <summary>
        /// Gets or sets the Content-Type HTTP header value.
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// Adds an HTTP header only if it doesn't already exist.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        void AddHeaderIfMissing(string name, string value);

        /// <summary>
        /// Gets a value indicating whether the connection is secure (HTTPS).
        /// </summary>
        bool IsSecureConnection { get; }
    }

    /// <summary>
    /// Production implementation that wraps HttpResponse.
    /// </summary>
    public class ResponseHeaderManager : IResponseHeaderManager
    {
        private readonly HttpResponse _response;
        private readonly HttpRequest _request;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseHeaderManager"/> class.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        public ResponseHeaderManager(HttpRequest request, HttpResponse response)
        {
            _request = request;
            _response = response;
        }

        /// <summary>
        /// Gets or sets the Content-Type HTTP header value.
        /// </summary>
        public string ContentType
        {
            get => _response.ContentType;
            set => _response.ContentType = value;
        }

        /// <summary>
        /// Gets a value indicating whether the connection is secure (HTTPS).
        /// </summary>
        public bool IsSecureConnection => _request?.IsSecureConnection ?? false;

        /// <summary>
        /// Adds an HTTP header only if it doesn't already exist.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        public void AddHeaderIfMissing(string name, string value)
        {
            // Validate parameters
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                value = string.Empty;
            }

            _response.AppendHeader(name, value);
        }
    }
}