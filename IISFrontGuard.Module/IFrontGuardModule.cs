using IISFrontGuard.Module.Models;
using System;
using System.Collections.Generic;
using System.Web;

namespace IISFrontGuard.Module
{
    /// <summary>
    /// Defines the contract for the FrontGuard HTTP Module that provides WAF, rate limiting, and security features.
    /// </summary>
    public interface IFrontGuardModule
    {
        /// <summary>
        /// Creates an HTTP cookie for the clearance token.
        /// </summary>
        /// <param name="newToken">The clearance token value.</param>
        /// <param name="expirationTime">The token expiration time.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>An HTTP cookie configured with secure settings.</returns>
        HttpCookie AddCookie(string newToken, DateTime expirationTime, HttpRequest request);

        /// <summary>
        /// Adds a clearance token to the cache with the specified expiration time.
        /// </summary>
        /// <param name="token">The clearance token.</param>
        /// <param name="expirationTime">The token expiration time in UTC.</param>
        void AddTokenToCache(string token, DateTime expirationTime);

        /// <summary>
        /// Blocks the HTTP request and displays an access denied page.
        /// </summary>
        /// <param name="request">The HTTP request to block.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="logContext">The logging context containing request metadata.</param>
        void BlockRequest(HttpRequest request, HttpResponse response, RequestLogContext logContext);

        /// <summary>
        /// Handles the BeginRequest event to process incoming HTTP requests.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void Context_BeginRequest(object sender, EventArgs e);

        /// <summary>
        /// Called when the application is being disposed/shutdown.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void Context_Disposed(object sender, EventArgs e);

        /// <summary>
        /// Called at the end of request processing to log the response details.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void Context_EndRequest(object sender, EventArgs e);

        /// <summary>
        /// Prepares the response by removing unnecessary headers and adding security headers.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void Context_PreSendRequestHeaders(object sender, EventArgs e);

        /// <summary>
        /// Creates a security event notification for a blocked request.
        /// </summary>
        /// <param name="request">The HTTP request that was blocked.</param>
        /// <param name="rule">The WAF rule that blocked the request.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <returns>A security event object describing the block event.</returns>
        SecurityEvent CreateBlockedEventNotification(HttpRequest request, WafRule rule, string rayId);

        /// <summary>
        /// Creates a security event notification for an issued challenge.
        /// </summary>
        /// <param name="request">The HTTP request that triggered the challenge.</param>
        /// <param name="rule">The WAF rule that issued the challenge.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="challengeType">The type of challenge (managed or interactive).</param>
        /// <returns>A security event object describing the challenge event.</returns>
        SecurityEvent CreateChallengeEventNotification(HttpRequest request, WafRule rule, string rayId, string challengeType);

        /// <summary>
        /// Determines the severity level based on the WAF rule's priority.
        /// </summary>
        /// <param name="rule">The WAF rule.</param>
        /// <returns>The severity level string.</returns>
        string DetermineSeverityFromRule(WafRule rule);

        /// <summary>
        /// Displays the challenge form to the user.
        /// </summary>
        /// <param name="context">The challenge context containing request/response information.</param>
        void DisplayChallengeForm(ChallengeContext context);

        /// <summary>
        /// Evaluates a single WAF condition against the HTTP request.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>True if the condition matches; otherwise, false.</returns>
        bool EvaluateCondition(WafCondition condition, HttpRequest request);

        /// <summary>
        /// Evaluates a WAF rule against the HTTP request using group-based logic.
        /// </summary>
        /// <param name="rule">The WAF rule to evaluate.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>True if the rule matches; otherwise, false.</returns>
        bool EvaluateRule(WafRule rule, HttpRequest request);

        /// <summary>
        /// Evaluates a collection of WAF conditions against the HTTP request.
        /// </summary>
        /// <param name="conditions">The conditions to evaluate.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>True if all conditions are satisfied; otherwise, false.</returns>
        [Obsolete("Use EvaluateRule with group-based logic instead. This method is for backward compatibility only.")]
        bool EvaluateConditions(IEnumerable<WafCondition> conditions, HttpRequest request);

        /// <summary>
        /// Fetches WAF conditions for a specific rule from the repository.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>A list of WAF conditions.</returns>
        List<WafCondition> FetchWafConditions(int ruleId, string connectionString);

        /// <summary>
        /// Fetches WAF rules for the specified host from the repository.
        /// </summary>
        /// <param name="host">The hostname to fetch rules for.</param>
        /// <returns>An enumerable collection of WAF rules.</returns>
        IEnumerable<WafRule> FetchWafRules(string host);

        /// <summary>
        /// Generates a new clearance token bound to the client's fingerprint and sets it as a cookie.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="key">The encryption key for the token.</param>
        void GenerateAndSetToken(HttpRequest request, HttpResponse response, string key);

        /// <summary>
        /// Generates a unique fingerprint for the client based on IP address and user agent.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>A base64-encoded SHA-256 hash of the client fingerprint.</returns>
        string GenerateClientFingerprint(HttpRequest request);

        /// <summary>
        /// Generates a CSRF token for challenge form protection.
        /// </summary>
        /// <param name="rayId">The unique request identifier.</param>
        /// <returns>A base64-encoded CSRF token.</returns>
        string GenerateCsrfToken(string rayId);

        /// <summary>
        /// Generates the HTML page for an interactive challenge (user must click checkbox).
        /// </summary>
        /// <param name="rootDomain">The domain name.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="csrfToken">The CSRF protection token.</param>
        /// <returns>The complete HTML page as a string.</returns>
        string GenerateHTMLInteractiveChallenge(string rootDomain, string rayId, string csrfToken);

        /// <summary>
        /// Generates the HTML page for a managed challenge (automatic verification after delay).
        /// </summary>
        /// <param name="rootDomain">The domain name.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="csrfToken">The CSRF protection token.</param>
        /// <returns>The complete HTML page as a string.</returns>
        string GenerateHTMLManagedChallenge(string rootDomain, string rayId, string csrfToken);

        /// <summary>
        /// Generates the HTML page displayed when rate limit is exceeded.
        /// </summary>
        /// <param name="rootDomain">The domain name.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <returns>The complete HTML rate limit page as a string.</returns>
        string GenerateHTMLRateLimitPage(string rootDomain, string rayId);

        /// <summary>
        /// Generates the HTML page displayed when a request is blocked.
        /// </summary>
        /// <param name="rootDomain">The domain name.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <returns>The complete HTML access denied page as a string.</returns>
        string GenerateHTMLUserBlockedPage(string rootDomain, string rayId);

        /// <summary>
        /// Retrieves an application setting as an integer value.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">The default value to return if the key is not found or cannot be parsed.</param>
        /// <returns>The configuration value as an integer, or the default value.</returns>
        int GetAppSettingAsInt(string key, int defaultValue);

        /// <summary>
        /// Gets the version number of the executing assembly.
        /// </summary>
        /// <returns>The assembly version string.</returns>
        string GetAssemblyVersion();

        /// <summary>
        /// Gets the length of the request body in bytes.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The body length as a string.</returns>
        string GetBodyLength(HttpRequest request);

        /// <summary>
        /// Gets the client IP address, considering proxy headers when behind trusted proxies.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The client IP address.</returns>
        string GetClientIp(HttpRequest request);

        /// <summary>
        /// Gets the database connection string for the specified request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The connection string.</returns>
        string GetConnectionString(HttpRequest request);

        /// <summary>
        /// Gets the database connection string for the specified host.
        /// </summary>
        /// <param name="host">The hostname.</param>
        /// <returns>The connection string.</returns>
        string GetConnectionStringByHost(string host);

        /// <summary>
        /// Gets the Content-Type header value from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The content type string.</returns>
        string GetContentType(HttpRequest request);

        /// <summary>
        /// Gets the continent name from the GeoIP context.
        /// </summary>
        /// <returns>The continent name, or empty string if unavailable.</returns>
        string GetContinentName();

        /// <summary>
        /// Gets a cookie value from the HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="name">The cookie name.</param>
        /// <returns>The cookie value, or empty string if not found.</returns>
        string GetCookieValue(HttpRequest request, string name);

        /// <summary>
        /// Gets the two-letter ISO country code from the GeoIP context.
        /// </summary>
        /// <returns>The ISO country code, or empty string if unavailable.</returns>
        string GetCountryIsoCode();

        /// <summary>
        /// Gets the country name from the GeoIP context.
        /// </summary>
        /// <returns>The country name, or empty string if unavailable.</returns>
        string GetCountryName();

        /// <summary>
        /// Extracts the value of a specified field from the HTTP request.
        /// </summary>
        /// <param name="field">The field identifier.</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="name">The field name (for cookies and headers).</param>
        /// <returns>The field value as a string.</returns>
        string GetFieldValue(byte field, HttpRequest request, string name = "");

        /// <summary>
        /// Gets the full absolute URI of the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The full URL.</returns>
        string GetFullUrl(HttpRequest request);

        /// <summary>
        /// Gets a specific HTTP header value from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="name">The header name.</param>
        /// <returns>The header value, or empty string if not found.</returns>
        string GetHeader(HttpRequest request, string name);

        /// <summary>
        /// Gets the hostname from the HTTP request URL.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The hostname.</returns>
        string GetHostname(HttpRequest request);

        /// <summary>
        /// Gets a host-specific connection string from configuration.
        /// </summary>
        /// <param name="host">The hostname to look up.</param>
        /// <returns>The host-specific connection string, or null if not configured.</returns>
        string GetHostSpecificConnectionString(string host);

        /// <summary>
        /// Gets the HTTP method (GET, POST, etc.) from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The HTTP method.</returns>
        string GetHttpMethod(HttpRequest request);

        /// <summary>
        /// Gets the HTTP version from the server variables.
        /// </summary>
        /// <returns>The HTTP version string.</returns>
        string GetHttpVersion();

        /// <summary>
        /// Gets the MIME type of the request from Content-Type header or file extension.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The MIME type string.</returns>
        string GetMimeType(HttpRequest request);

        /// <summary>
        /// Gets the protocol (http or https) from the HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The protocol string.</returns>
        string GetProtocol(HttpRequest request);

        /// <summary>
        /// Gets the query string from the request URL.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The query string.</returns>
        string GetQueryString(HttpRequest request);

        /// <summary>
        /// Gets the referrer URL from the HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The referrer URL, or empty string if not provided.</returns>
        string GetReferrer(HttpRequest request);

        /// <summary>
        /// Gets the URL path (without query string) from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The URL path.</returns>
        string GetUrlPath(HttpRequest request);

        /// <summary>
        /// Gets the URL path and query string from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The URL path and query string.</returns>
        string GetUrlPathAndQuery(HttpRequest request);

        /// <summary>
        /// Gets the user agent string from the HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The user agent string.</returns>
        string GetUserAgent(HttpRequest request);

        /// <summary>
        /// Gets the X-Forwarded-For header value.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The X-Forwarded-For value, or empty string if not present.</returns>
        string GetXForwardedFor(HttpRequest request);

        /// <summary>
        /// Handles CSRF token validation failure during challenge processing.
        /// </summary>
        /// <param name="context">The challenge context containing request/response information.</param>
        void HandleCsrfValidationFailure(ChallengeContext context);

        /// <summary>
        /// Handles an interactive challenge (user must click to verify).
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="token">The clearance token from the client's cookies.</param>
        /// <param name="key">The encryption key for token validation.</param>
        /// <param name="logContext">The logging context containing request metadata.</param>
        void HandleInteractiveChallenge(HttpRequest request, HttpResponse response, string token, string key, RequestLogContext logContext);

        /// <summary>
        /// Handles a managed challenge (automatic verification after delay).
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="token">The clearance token from the client's cookies.</param>
        /// <param name="key">The encryption key for token validation.</param>
        /// <param name="logContext">The logging context containing request metadata.</param>
        void HandleManagedChallenge(HttpRequest request, HttpResponse response, string token, string key, RequestLogContext logContext);

        /// <summary>
        /// Handles the action specified by a WAF rule (skip, block, challenge, or log).
        /// </summary>
        /// <param name="rule">The WAF rule that was matched.</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="iso2">The two-letter ISO country code of the client.</param>
        void HandleRuleAction(WafRule rule, HttpRequest request, HttpResponse response, string rayId, string iso2);

        /// <summary>
        /// Determines whether a client IP address has exceeded the rate limit.
        /// </summary>
        /// <param name="clientIp">The client IP address to check.</param>
        /// <param name="maxRequests">The maximum number of requests allowed within the time window.</param>
        /// <param name="windowSeconds">The time window in seconds.</param>
        /// <returns>True if the client is rate limited; otherwise, false.</returns>
        bool IsRateLimited(string clientIp, int maxRequests = 100, int windowSeconds = 60);

        /// <summary>
        /// Validates whether a clearance token is valid and not expired.
        /// </summary>
        /// <param name="token">The clearance token to validate.</param>
        /// <param name="request">The HTTP request (optional, used for fingerprint validation).</param>
        /// <param name="key">The encryption key for token validation.</param>
        /// <returns>True if the token is valid; otherwise, false.</returns>
        bool IsTokenValid(string token, HttpRequest request = null, string key = "");

        /// <summary>
        /// Logs the request and allows it to proceed without blocking.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="logContext">The logging context containing request metadata.</param>
        void LogAndProceed(HttpRequest request, RequestLogContext logContext);

        /// <summary>
        /// Sends a security event notification for a suspected token replay attack.
        /// </summary>
        /// <param name="request">The HTTP request with the mismatched token.</param>
        void NotifyTokenReplayAttempt(HttpRequest request);

        /// <summary>
        /// Processes a POST request from a challenge form.
        /// </summary>
        /// <param name="context">The challenge context containing request/response information.</param>
        void ProcessChallengePostRequest(ChallengeContext context);

        /// <summary>
        /// Removes unnecessary server information headers from the HTTP response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        void RemoveUnnecessaryHeaders(HttpResponse response);

        /// <summary>
        /// Sends a security event notification via webhook.
        /// </summary>
        /// <param name="securityEvent">The security event to send.</param>
        void SendSecurityEventNotification(SecurityEvent securityEvent);

        /// <summary>
        /// Starts timing the request processing duration.
        /// </summary>
        /// <param name="app">The HTTP application instance.</param>
        void StartRequestTiming(HttpApplication app);

        /// <summary>
        /// Tracks challenge failures for a client IP to detect brute force attempts.
        /// </summary>
        /// <param name="clientIp">The client IP address.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="reason">The failure reason.</param>
        void TrackChallengeFailure(string clientIp, string rayId, string reason);

        /// <summary>
        /// Validates a submitted CSRF token against the cached value.
        /// </summary>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="submittedToken">The CSRF token submitted by the client.</param>
        /// <returns>True if the token is valid; otherwise, false.</returns>
        bool ValidateCsrfToken(string rayId, string submittedToken);

        /// <summary>
        /// Validates that the token's embedded fingerprint matches the current client's fingerprint.
        /// </summary>
        /// <param name="token">The encrypted clearance token.</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="key">The encryption key.</param>
        /// <returns>True if the fingerprint matches; otherwise, false.</returns>
        bool ValidateTokenFingerprint(string token, HttpRequest request, string key);

        /// <summary>
        /// Initializes the HTTP module and subscribes to application events.
        /// </summary>
        /// <param name="context">The HTTP application instance.</param>
        void Init(HttpApplication context);
    }
}