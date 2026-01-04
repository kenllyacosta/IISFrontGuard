using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using MaxMind.GeoIP2.Responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;

namespace IISFrontGuard.Module
{
    /// <summary>
    /// IIS HTTP Module that provides Web Application Firewall (WAF) functionality, rate limiting,
    /// CSRF protection, challenge-response mechanisms, and security event logging.
    /// </summary>
    public class FrontGuardModule : IHttpModule, IFrontGuardModule
    {
        private const string TokenKey = "fgm_clearance";
        private const string _fallbackConnectionString = "Data Source=.;Initial Catalog=IISFrontGuard;Integrated Security=True;TrustServerCertificate=True;";
        private const string _fallbackEncryptionKey = "1234567890123456";
        private const int RateLimitMaxRequestsPerMinute = 150;
        private const int RateLimitWindowSeconds = 60;
        private const string ContentTypeTextHtml = "text/html";
        private const string GeoInfoContextKey = "IISFrontGuard.GeoInfo";
        private const string WebHookEnabledAppSettingKey = "IISFrontGuard.Webhook.Enabled";

        // Dependencies
        private readonly IRequestLogger _requestLogger;
        private readonly IWebhookNotifier _webhookNotifier;
        private readonly IGeoIPService _geoIPService;
        private readonly IWafRuleRepository _wafRuleRepository;
        private readonly ICacheProvider _tokenCache;
        private readonly IConfigurationProvider _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitCache;
        private readonly ConcurrentDictionary<string, ChallengeFailureInfo> _challengeFailures;

        /// <summary>
        /// Indicates whether webhook notifications are enabled for security events.
        /// </summary>
        public readonly bool webhookEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontGuardModule"/> class with default production implementations.
        /// This constructor is used by IIS when the module is loaded.
        /// </summary>
        public FrontGuardModule() : this(
            new RequestLoggerAdapter(),
            new WebhookNotifierAdapter(),
            new GeoIPServiceAdapter(@"~/GeoLite2-Country.mmdb"),
            new WafRuleRepository(new HttpRuntimeCacheProvider()),
            new HttpRuntimeCacheProvider(),
            new AppConfigConfigurationProvider(),
            new HttpContextAccessor())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontGuardModule"/> class with dependency injection support.
        /// This constructor is primarily used for testing purposes.
        /// </summary>
        /// <param name="requestLogger">The request logger service.</param>
        /// <param name="webhookNotifier">The webhook notifier service.</param>
        /// <param name="geoIPService">The GeoIP lookup service.</param>
        /// <param name="wafRuleRepository">The WAF rule repository.</param>
        /// <param name="tokenCache">The token cache provider.</param>
        /// <param name="configuration">The configuration provider.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
        public FrontGuardModule(IRequestLogger requestLogger, IWebhookNotifier webhookNotifier, IGeoIPService geoIPService, IWafRuleRepository wafRuleRepository, ICacheProvider tokenCache, IConfigurationProvider configuration, IHttpContextAccessor httpContextAccessor)
        {
            _requestLogger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));
            _webhookNotifier = webhookNotifier ?? throw new ArgumentNullException(nameof(webhookNotifier));
            _geoIPService = geoIPService;
            _wafRuleRepository = wafRuleRepository ?? throw new ArgumentNullException(nameof(wafRuleRepository));
            _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            _rateLimitCache = new ConcurrentDictionary<string, RateLimitInfo>();
            _challengeFailures = new ConcurrentDictionary<string, ChallengeFailureInfo>();

            webhookEnabled = _configuration.GetAppSettingAsBool(WebHookEnabledAppSettingKey, false);
        }

        /// <summary>
        /// Initializes the HTTP module and subscribes to application events.
        /// </summary>
        /// <param name="context">The HTTP application instance.</param>
        public void Init(HttpApplication context)
        {
            context.BeginRequest += Context_BeginRequest;
            context.EndRequest += Context_EndRequest;
            context.PreSendRequestHeaders += Context_PreSendRequestHeaders;
            context.Disposed += Context_Disposed;
        }

        /// <summary>
        /// Called when the application is being disposed/shutdown
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        public void Context_Disposed(object sender, EventArgs e)
        {
            try
            {
                // Stop background services gracefully
                _webhookNotifier.Stop();
                _requestLogger.Stop();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"FrontGuardModule.Context_Disposed error: {ex}");
                // Swallow to avoid bringing down the worker process
            }
        }

        /// <summary>
        /// Handles the BeginRequest event to process incoming HTTP requests.
        /// Performs rate limiting, GeoIP resolution, and WAF rule evaluation.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        public void Context_BeginRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var request = app.Context.Request;
            var response = app.Context.Response;
            string clientIp = GetClientIp(request);

            string rayId = Guid.NewGuid().ToString();
            int rateLimitMaxRequestsPerMinute = GetAppSettingAsInt("IISFrontGuard.RateLimitMaxRequestsPerMinute", RateLimitMaxRequestsPerMinute);
            int rateLimitWindowSeconds = GetAppSettingAsInt("IISFrontGuard.RateLimitWindowSeconds", RateLimitWindowSeconds);

            // Rate limiting check
            if (IsRateLimited(clientIp, rateLimitMaxRequestsPerMinute, rateLimitWindowSeconds))
            {
                // WEBHOOK: Rate limit exceeded
                if (GetAppSettingAsBool(WebHookEnabledAppSettingKey, false))
                    SendSecurityEventNotification(new SecurityEvent
                    {
                        EventType = SecurityEventTypes.RateLimitExceeded,
                        Severity = SecurityEventSeverity.High,
                        Timestamp = DateTime.UtcNow,
                        RayId = rayId,
                        ClientIp = clientIp,
                        HostName = request.Url.Host,
                        UserAgent = request.UserAgent,
                        Url = request.Url.ToString(),
                        HttpMethod = request.HttpMethod,
                        Description = $"Client exceeded rate limit: {rateLimitMaxRequestsPerMinute} requests per {rateLimitWindowSeconds}s",
                        AdditionalData = new { max_requests = rateLimitMaxRequestsPerMinute, window_seconds = rateLimitWindowSeconds }
                    });

                response.StatusCode = 200;
                response.ContentType = ContentTypeTextHtml;
                response.Write(GenerateHTMLRateLimitPage(request.Url.Host, rayId));
                _httpContextAccessor.CompleteRequest();
                return;
            }

            _httpContextAccessor.SetContextItem("RayId", rayId);

            string clientIpString = app.Context.Request.UserHostAddress;
            string iso2 = "00";

            // Resolve geo info per-request and store it in HttpContext.Items so it is not shared across requests
            CountryResponse requestGeoInfo = null;

            if (_geoIPService != null)
                requestGeoInfo = _geoIPService.GetGeoInfo(clientIpString);
            else
                requestGeoInfo = new CountryResponse();

            _httpContextAccessor.SetContextItem(GeoInfoContextKey, requestGeoInfo);

            if (clientIpString != "::1" && clientIpString != "127.0.0.1" && requestGeoInfo?.Country != null)
            {
                iso2 = requestGeoInfo.Country.IsoCode ?? iso2;
            }

            var cs = GetConnectionString(request);
            _requestLogger.Enqueue(request, cs, null, rayId, iso2, 6, null); //Traffic logged

            var rules = FetchWafRules(request.Url.Host);

            foreach (var rule in rules.OrderBy(r => r.Prioridad))
            {
                if (!rule.Habilitado)
                    continue;

                if (EvaluateConditions(rule.Conditions, request))
                {
                    HandleRuleAction(rule, request, response, rayId, iso2);
                    break;
                }
            }

            StartRequestTiming(app);
        }

        /// <summary>
        /// Determines whether a client IP address has exceeded the rate limit.
        /// </summary>
        /// <param name="clientIp">The client IP address to check.</param>
        /// <param name="maxRequests">The maximum number of requests allowed within the time window.</param>
        /// <param name="windowSeconds">The time window in seconds.</param>
        /// <returns><c>true</c> if the client is rate limited; otherwise, <c>false</c>.</returns>
        public bool IsRateLimited(string clientIp, int maxRequests = 100, int windowSeconds = 60)
        {
            var now = DateTime.UtcNow;
            var info = _rateLimitCache.GetOrAdd(clientIp, _ => new RateLimitInfo { WindowStart = now });

            lock (info)
            {
                if ((now - info.WindowStart).TotalSeconds > windowSeconds)
                {
                    info.RequestCount = 1;
                    info.WindowStart = now;
                    return false;
                }

                info.RequestCount++;
                return info.RequestCount > maxRequests;
            }
        }

        /// <summary>
        /// Retrieves an application setting as an integer value.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">The default value to return if the key is not found or cannot be parsed.</param>
        /// <returns>The configuration value as an integer, or the default value.</returns>
        public int GetAppSettingAsInt(string key, int defaultValue)
        {
            int result = _configuration.GetAppSettingAsInt(key, defaultValue);
            if (result == 0)
                int.TryParse(ConfigurationManager.AppSettings[key], out result);

            return result;
        }

        /// <summary>
        /// Retrieves an application setting as a boolean value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public bool GetAppSettingAsBool(string key, bool defaultValue)
        {
            bool result = _configuration.GetAppSettingAsBool(key, defaultValue);
            if (!result)
                bool.TryParse(ConfigurationManager.AppSettings[key], out result);

            return result;
        }

        /// <summary>
        /// Handles the action specified by a WAF rule (skip, block, challenge, or log).
        /// </summary>
        /// <param name="rule">The WAF rule that was matched.</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="iso2">The two-letter ISO country code of the client.</param>
        public void HandleRuleAction(WafRule rule, HttpRequest request, HttpResponse response, string rayId, string iso2)
        {
            var token = request.Cookies[TokenKey]?.Value;
            var cs = GetConnectionString(request);
            var key = _configuration.GetAppSetting("IISFrontGuardEncryptionKey");
            if (string.IsNullOrEmpty(key))
                key = _fallbackEncryptionKey;

            var logContext = new RequestLogContext
            {
                RuleTriggered = rule.Id,
                ConnectionString = cs,
                RayId = rayId,
                Iso2 = iso2,
                ActionId = rule.ActionId,
                AppId = rule.AppId.ToString()
            };

            switch (rule.ActionId)
            {
                case 1: // "skip":
                    LogAndProceed(request, logContext);
                    break;
                case 2: // "block":

                    if (GetAppSettingAsBool(WebHookEnabledAppSettingKey, false))
                    {
                        SendSecurityEventNotification(CreateBlockedEventNotification(request, rule, rayId));
                    }

                    BlockRequest(request, response, logContext);
                    break;
                case 3: // "managed challenge":
                    if (string.IsNullOrEmpty(token) && webhookEnabled)
                    {
                        SendSecurityEventNotification(CreateChallengeEventNotification(request, rule, rayId, "managed"));
                    }

                    HandleManagedChallenge(request, response, token, key, logContext);
                    break;
                case 4: // "interactive challenge":
                    if (string.IsNullOrEmpty(token) && webhookEnabled)
                    {
                        SendSecurityEventNotification(CreateChallengeEventNotification(request, rule, rayId, "interactive"));
                    }

                    HandleInteractiveChallenge(request, response, token, key, logContext);
                    break;
                case 5: // "log":
                    LogAndProceed(request, logContext);
                    break;
            }
        }

        /// <summary>
        /// Creates a security event notification for a blocked request.
        /// </summary>
        /// <param name="request">The HTTP request that was blocked.</param>
        /// <param name="rule">The WAF rule that blocked the request.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <returns>A <see cref="SecurityEvent"/> object describing the block event.</returns>
        public SecurityEvent CreateBlockedEventNotification(HttpRequest request, WafRule rule, string rayId)
        {
            return new SecurityEvent
            {
                EventType = SecurityEventTypes.RequestBlocked,
                Severity = DetermineSeverityFromRule(rule),
                Timestamp = DateTime.UtcNow,
                RayId = rayId,
                ClientIp = GetClientIp(request),
                HostName = request.Url.Host,
                UserAgent = request.UserAgent,
                Url = request.Url.ToString(),
                HttpMethod = request.HttpMethod,
                RuleId = rule.Id,
                RuleName = rule.Nombre,
                Description = $"Request blocked by WAF rule: {rule.Nombre}",
                AdditionalData = new { rule_priority = rule.Prioridad, conditions = rule.Conditions?.Count }
            };
        }

        /// <summary>
        /// Creates a security event notification for an issued challenge.
        /// </summary>
        /// <param name="request">The HTTP request that triggered the challenge.</param>
        /// <param name="rule">The WAF rule that issued the challenge.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="challengeType">The type of challenge (managed or interactive).</param>
        /// <returns>A <see cref="SecurityEvent"/> object describing the challenge event.</returns>
        public SecurityEvent CreateChallengeEventNotification(HttpRequest request, WafRule rule, string rayId, string challengeType)
        {
            return new SecurityEvent
            {
                EventType = SecurityEventTypes.ChallengeIssued,
                Severity = SecurityEventSeverity.Medium,
                Timestamp = DateTime.UtcNow,
                RayId = rayId,
                ClientIp = GetClientIp(request),
                HostName = request.Url.Host,
                UserAgent = request.UserAgent,
                Url = request.Url.ToString(),
                HttpMethod = request.HttpMethod,
                RuleId = rule.Id,
                RuleName = rule.Nombre,
                Description = $"{char.ToUpper(challengeType[0]) + challengeType.Substring(1)} challenge issued by rule: {rule.Nombre}",
                AdditionalData = new { challenge_type = challengeType }
            };
        }

        /// <summary>
        /// Logs the request and allows it to proceed without blocking.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="logContext">The logging context containing request metadata.</param>
        public void LogAndProceed(HttpRequest request, RequestLogContext logContext)
            => _requestLogger.Enqueue(request, logContext.ConnectionString, logContext.RuleTriggered, logContext.RayId, logContext.Iso2, logContext.ActionId, logContext.AppId);

        /// <summary>
        /// Blocks the HTTP request and displays an access denied page.
        /// </summary>
        /// <param name="request">The HTTP request to block.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="logContext">The logging context containing request metadata.</param>
        public void BlockRequest(HttpRequest request, HttpResponse response, RequestLogContext logContext)
        {
            _requestLogger.Enqueue(request, logContext.ConnectionString, logContext.RuleTriggered, logContext.RayId, logContext.Iso2, logContext.ActionId, logContext.AppId);
            response.ContentType = ContentTypeTextHtml;
            response.StatusCode = 200;
            response.Write(GenerateHTMLUserBlockedPage(request.Url.Host, logContext.RayId));
            _httpContextAccessor.CompleteRequest();
        }

        /// <summary>
        /// Handles a managed challenge (automatic verification after delay).
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="token">The clearance token from the client's cookies.</param>
        /// <param name="key">The encryption key for token validation.</param>
        /// <param name="logContext">The logging context containing request metadata.</param>
        public void HandleManagedChallenge(HttpRequest request, HttpResponse response, string token, string key, RequestLogContext logContext)
        {
            if (string.IsNullOrEmpty(token) || !IsTokenValid(token, request, key))
            {
                var challengeContext = new ChallengeContext
                {
                    Request = request,
                    Response = response,
                    Token = token,
                    Key = key,
                    LogContext = logContext,
                    HtmlGenerator = GenerateHTMLManagedChallenge
                };

                if (request.HttpMethod == "POST")
                {
                    ProcessChallengePostRequest(challengeContext);
                }
                else
                {
                    DisplayChallengeForm(challengeContext);
                }
            }
        }

        /// <summary>
        /// Handles an interactive challenge (user must click to verify).
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="token">The clearance token from the client's cookies.</param>
        /// <param name="key">The encryption key for token validation.</param>
        /// <param name="logContext">The logging context containing request metadata.</param>
        public void HandleInteractiveChallenge(HttpRequest request, HttpResponse response, string token, string key, RequestLogContext logContext)
        {
            if (string.IsNullOrEmpty(token) || !IsTokenValid(token, request, key))
            {
                var challengeContext = new ChallengeContext
                {
                    Request = request,
                    Response = response,
                    Token = token,
                    Key = key,
                    LogContext = logContext,
                    HtmlGenerator = GenerateHTMLInteractiveChallenge
                };

                if (request.HttpMethod == "POST")
                {
                    ProcessChallengePostRequest(challengeContext);
                }
                else
                {
                    DisplayChallengeForm(challengeContext);
                }
            }
        }

        /// <summary>
        /// Processes a POST request from a challenge form.
        /// </summary>
        /// <param name="context">The challenge context containing request/response information.</param>
        public void ProcessChallengePostRequest(ChallengeContext context)
        {
            var formRayId = context.Request.Form["__rayId"];
            var submittedCsrf = context.Request.Form["__csrf"];

            if (!string.IsNullOrEmpty(formRayId) && ValidateCsrfToken(formRayId, submittedCsrf))
            {
                GenerateAndSetToken(context.Request, context.Response, context.Key);
            }
            else
            {
                HandleCsrfValidationFailure(context);
            }
        }

        /// <summary>
        /// Displays the challenge form to the user.
        /// </summary>
        /// <param name="context">The challenge context containing request/response information.</param>
        public void DisplayChallengeForm(ChallengeContext context)
        {
            var cs = GetConnectionString(context.Request);
            _requestLogger.Enqueue(context.Request, cs, context.LogContext.RuleTriggered, context.LogContext.RayId, context.LogContext.Iso2, context.LogContext.ActionId, context.LogContext.AppId);
            context.Response.StatusCode = 200;
            context.Response.ContentType = ContentTypeTextHtml;

            var csrfToken = GenerateCsrfToken(context.LogContext.RayId);
            context.Response.Write(context.HtmlGenerator(context.Request.Url.Host, context.LogContext.RayId, csrfToken));
            _httpContextAccessor.CompleteRequest();
        }

        /// <summary>
        /// Handles CSRF token validation failure during challenge processing.
        /// </summary>
        /// <param name="context">The challenge context containing request/response information.</param>
        public void HandleCsrfValidationFailure(ChallengeContext context)
        {
            var clientIp = GetClientIp(context.Request);
            TrackChallengeFailure(clientIp, context.LogContext.RayId, "CSRF validation failed");

            if (GetAppSettingAsBool(WebHookEnabledAppSettingKey, false))
            {
                SendSecurityEventNotification(new SecurityEvent
                {
                    EventType = SecurityEventTypes.CSRFTokenMismatch,
                    Severity = SecurityEventSeverity.High,
                    Timestamp = DateTime.UtcNow,
                    RayId = context.LogContext.RayId,
                    ClientIp = clientIp,
                    HostName = context.Request.Url.Host,
                    UserAgent = context.Request.UserAgent,
                    Url = context.Request.Url.ToString(),
                    HttpMethod = context.Request.HttpMethod,
                    Description = "CSRF token validation failed during challenge"
                });
            }

            var cs = GetConnectionString(context.Request);
            _requestLogger.Enqueue(context.Request, cs, context.LogContext.RuleTriggered, context.LogContext.RayId, context.LogContext.Iso2, context.LogContext.ActionId, context.LogContext.AppId);
            context.Response.StatusCode = 200;
            context.Response.ContentType = ContentTypeTextHtml;
            var csrfToken = GenerateCsrfToken(context.LogContext.RayId);
            context.Response.Write(context.HtmlGenerator(context.Request.Url.Host, context.LogContext.RayId, csrfToken));
            _httpContextAccessor.CompleteRequest();
        }

        /// <summary>
        /// Generates a CSRF token for challenge form protection.
        /// </summary>
        /// <param name="rayId">The unique request identifier.</param>
        /// <returns>A base64-encoded CSRF token.</returns>
        public string GenerateCsrfToken(string rayId)
        {
            var token = $"{rayId}:{DateTime.UtcNow.Ticks}";
            var hash = System.Security.Cryptography.SHA256.Create()
                .ComputeHash(Encoding.UTF8.GetBytes(token));
            var csrfToken = Convert.ToBase64String(hash);
            // Cache provider expects local absolute expiration - convert from UTC to local
            var absoluteExpiration = DateTime.UtcNow.AddMinutes(5).ToLocalTime();

            _tokenCache.Insert($"CSRF_{rayId}", csrfToken, null, absoluteExpiration, Cache.NoSlidingExpiration);

            return csrfToken;
        }

        /// <summary>
        /// Validates a submitted CSRF token against the cached value.
        /// </summary>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="submittedToken">The CSRF token submitted by the client.</param>
        /// <returns><c>true</c> if the token is valid; otherwise, <c>false</c>.</returns>
        public bool ValidateCsrfToken(string rayId, string submittedToken)
            => _tokenCache.Get($"CSRF_{rayId}") is string stored && stored == submittedToken;

        /// <summary>
        /// Generates a new clearance token bound to the client's fingerprint and sets it as a cookie.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="response">The HTTP response.</param>
        /// <param name="key">The encryption key for the token.</param>
        public void GenerateAndSetToken(HttpRequest request, HttpResponse response, string key)
        {
            // Use cryptographically secure random token
            var tokenBytes = new byte[32];
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(tokenBytes);
            }
            var rawToken = Convert.ToBase64String(tokenBytes);

            // Bind token to client fingerprint (IP + User-Agent)
            var clientFingerprint = GenerateClientFingerprint(request);
            var tokenWithFingerprint = $"{rawToken}|{clientFingerprint}";
            var newToken = _requestLogger.Encrypt(tokenWithFingerprint, key);

            var expirationTime = DateTime.UtcNow.Add(TimeSpan.FromHours(_requestLogger.GetTokenExpirationDuration(request.Url.Host, GetConnectionString(request))));

            AddTokenToCache(newToken, expirationTime);

            response.AppendCookie(AddCookie(newToken, expirationTime, request));

            if (IsTokenValid(newToken, request, key))
            {
                response.Redirect(request.Url.AbsolutePath, false);
                _httpContextAccessor.CompleteRequest();
            }
        }

        /// <summary>
        /// Generates a unique fingerprint for the client based on IP address and user agent.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>A base64-encoded SHA-256 hash of the client fingerprint.</returns>
        public string GenerateClientFingerprint(HttpRequest request)
        {
            var clientIp = GetClientIp(request);
            var userAgent = request.UserAgent ?? string.Empty;

            // Create hash of IP + User-Agent
            var fingerprint = $"{clientIp}|{userAgent}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprint));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Validates whether a clearance token is valid and not expired.
        /// </summary>
        /// <param name="token">The clearance token to validate.</param>
        /// <param name="request">The HTTP request (optional, used for fingerprint validation).</param>
        /// <param name="key">The encryption key.</param>
        /// <returns><c>true</c> if the token is valid; otherwise, <c>false</c>.</returns>
        public bool IsTokenValid(string token, HttpRequest request = null, string key = "")
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            var cached = _tokenCache.Get(token);
            if (!(cached is DateTime expirationTime))
            {
                return false;
            }

            if (DateTime.UtcNow > expirationTime)
            {
                return false;
            }

            if (request == null)
            {
                return true;
            }

            return ValidateTokenFingerprint(token, request, key);
        }

        /// <summary>
        /// Validates that the token's embedded fingerprint matches the current client's fingerprint.
        /// </summary>
        /// <param name="token">The encrypted clearance token.</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="key">The encryption key.</param>
        /// <returns><c>true</c> if the fingerprint matches; otherwise, <c>false</c>.</returns>
        public bool ValidateTokenFingerprint(string token, HttpRequest request, string key)
        {
            var decrypted = _requestLogger.Decrypt(token, key);

            if (string.IsNullOrEmpty(decrypted))
            {
                return false;
            }

            var parts = decrypted.Split('|');
            if (parts.Length != 2)
            {
                return false;
            }

            var storedFingerprint = parts[1];
            var currentFingerprint = GenerateClientFingerprint(request);

            if (storedFingerprint != currentFingerprint)
            {
                NotifyTokenReplayAttempt(request);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sends a security event notification for a suspected token replay attack.
        /// </summary>
        /// <param name="request">The HTTP request with the mismatched token.</param>
        public void NotifyTokenReplayAttempt(HttpRequest request)
        {
            if (!webhookEnabled)
            {
                return;
            }

            SendSecurityEventNotification(new SecurityEvent
            {
                EventType = SecurityEventTypes.TokenReplayAttempt,
                Severity = SecurityEventSeverity.Critical,
                Timestamp = DateTime.UtcNow,
                RayId = Guid.NewGuid().ToString(),
                ClientIp = GetClientIp(request),
                HostName = request.Url.Host,
                UserAgent = request.UserAgent,
                Url = request.Url.ToString(),
                HttpMethod = request.HttpMethod,
                Description = "Token fingerprint mismatch - possible token replay attack"
            });
        }

        /// <summary>
        /// Starts timing the request processing duration.
        /// </summary>
        /// <param name="app">The HTTP application instance.</param>
        public void StartRequestTiming(HttpApplication app)
            => app.Context.Items["RequestStartTime"] = Stopwatch.StartNew();

        /// <summary>
        /// Creates an HTTP cookie for the clearance token.
        /// </summary>
        /// <param name="newToken">The clearance token value.</param>
        /// <param name="expirationTime">The token expiration time.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>An <see cref="HttpCookie"/> configured with secure settings.</returns>
        public HttpCookie AddCookie(string newToken, DateTime expirationTime, HttpRequest request)
        {
            return new HttpCookie(TokenKey, newToken)
            {
                Secure = request.IsSecureConnection,
                // HttpCookie.Expires uses local time; ensure proper conversion
                Expires = expirationTime.ToLocalTime(),
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/", // Restrict to root path
                Domain = null // Don't set domain to prevent subdomain sharing
            };
        }

        /// <summary>
        /// Fetches WAF rules for the specified host from the repository.
        /// </summary>
        /// <param name="host">The hostname to fetch rules for.</param>
        /// <returns>An enumerable collection of <see cref="WafRule"/> objects.</returns>
        public IEnumerable<WafRule> FetchWafRules(string host)
        {
            var connectionString = GetConnectionStringByHost(host);
            return _wafRuleRepository.FetchWafRules(host, connectionString);
        }

        /// <summary>
        /// Fetches WAF conditions for a specific rule from the repository.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>A list of <see cref="WafCondition"/> objects.</returns>
        public List<WafCondition> FetchWafConditions(int ruleId, string connectionString)
        {
            return _wafRuleRepository.FetchWafConditions(ruleId, connectionString);
        }

        /// <summary>
        /// Evaluates a collection of WAF conditions against the HTTP request.
        /// </summary>
        /// <param name="conditions">The conditions to evaluate.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns><c>true</c> if all conditions are satisfied; otherwise, <c>false</c>.</returns>
        public bool EvaluateConditions(IEnumerable<WafCondition> conditions, HttpRequest request)
        {
            // Evaluate conditions against the request
            bool result = true;
            foreach (var condition in conditions)
            {
                bool match = EvaluateCondition(condition, request);
                if (condition.LogicOperator == 1 && !match) // AND
                    return false;

                if (condition.LogicOperator == 2 && match) // OR
                    return true;
            }
            return result;
        }

        /// <summary>
        /// Evaluates a single WAF condition against the HTTP request.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns><c>true</c> if the condition matches; otherwise, <c>false</c>.</returns>
        public bool EvaluateCondition(WafCondition condition, HttpRequest request)
        {
            // Evaluate a single condition
            string fieldValue = GetFieldValue(condition.FieldId, request, condition.FieldName?.ToLower()).ToLower();
            switch (condition.OperatorId)
            {
                case 1: // "equals":
                    return fieldValue == condition.Valor;
                case 2: // "does not equals":
                    return fieldValue != condition.Valor;
                case 3: // "contains":
                    return fieldValue.Contains(condition.Valor);
                case 4: // "does not contain":
                    return !fieldValue.Contains(condition.Valor);
                case 5: // "matches regex":
                    return Regex.IsMatch(fieldValue, condition.Valor, RegexOptions.None, TimeSpan.FromSeconds(2));
                case 6: // "does not match regex":
                    return !Regex.IsMatch(fieldValue, condition.Valor, RegexOptions.None, TimeSpan.FromSeconds(2));
                case 7: // "starts with":
                    return fieldValue.StartsWith(condition.Valor, StringComparison.OrdinalIgnoreCase);
                case 8: // "does not start with":
                    return !fieldValue.StartsWith(condition.Valor, StringComparison.OrdinalIgnoreCase);
                case 9: // "ends with":
                    return fieldValue.EndsWith(condition.Valor, StringComparison.OrdinalIgnoreCase);
                case 10: // "does not end with":
                    return !fieldValue.EndsWith(condition.Valor, StringComparison.OrdinalIgnoreCase);
                case 11: // "is in":
                    var valuesIn = condition.Valor.Split(',').Select(v => v.Trim());
                    return valuesIn.Contains(fieldValue);
                case 12: // "is not in":
                    var valuesNotIn = condition.Valor.Split(',').Select(v => v.Trim());
                    return !valuesNotIn.Contains(fieldValue);
                case 13: // "is in list":
                    var valuesInList = condition.Valor.Split(',').Select(v => v.Trim()).ToList();
                    return valuesInList.Contains(fieldValue);
                case 14: // "is not in list":
                    var valuesNotInList = condition.Valor.Split(',').Select(v => v.Trim()).ToList();
                    return !valuesNotInList.Contains(fieldValue);
                case 15: // "is ip in range":
                    IpValidator ipValidator = new IpValidator(condition.Valor.Split(','));
                    return ipValidator.IsInIp(fieldValue);
                case 16: // "is ip not in range":
                    IpValidator ipValidatorNot = new IpValidator(condition.Valor.Split(','));
                    return !ipValidatorNot.IsInIp(fieldValue);
                case 17: // "greater than":
                    if (long.TryParse(fieldValue, out var fieldLong) && long.TryParse(condition.Valor, out var conditionLong))
                        return fieldLong > conditionLong;
                    return false;
                case 18: // "less than":
                    if (long.TryParse(fieldValue, out var fieldLongLt) && long.TryParse(condition.Valor, out var conditionLongLt))
                        return fieldLongLt < conditionLongLt;
                    return false;
                case 19: // "greater than or equal to"
                    if (long.TryParse(fieldValue, out var fieldLongGte) && long.TryParse(condition.Valor, out var conditionLongGte))
                        return fieldLongGte >= conditionLongGte;
                    return false;
                case 20: // "less than or equal to"
                    if (long.TryParse(fieldValue, out var fieldLongLte) && long.TryParse(condition.Valor, out var conditionLongLte))
                        return fieldLongLte <= conditionLongLte;
                    return false;
                case 21: // "is present"
                    return !string.IsNullOrEmpty(fieldValue);
                case 22: // "is not present"
                    return string.IsNullOrEmpty(fieldValue);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Extracts the value of a specified field from the HTTP request.
        /// </summary>
        /// <param name="field">The field identifier.</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="name">The field name (for cookies and headers).</param>
        /// <returns>The field value as a string.</returns>
        public string GetFieldValue(byte field, HttpRequest request, string name = "")
        {
            // Extract the value of the specified field from the request
            switch (field)
            {
                case 1: // cookie
                    return GetCookieValue(request, name);
                case 2: // hostname
                    return GetHostname(request);
                case 3: // ip
                case 4: // ip-range
                    return GetClientIp(request);
                case 5: // protocol
                    return GetProtocol(request);
                case 6: // referrer
                    return GetReferrer(request);
                case 7: // method
                    return GetHttpMethod(request);
                case 8: // httpversion
                    return GetHttpVersion();
                case 9: // user-agent
                    return GetUserAgent(request);
                case 10: // x-forwarded-for
                    return GetXForwardedFor(request);
                case 11: // mimetype
                    return GetMimeType(request);
                case 12: // url-full
                    return GetFullUrl(request);
                case 13: // url
                    return GetUrlPath(request);
                case 14: // url-path
                    return GetUrlPathAndQuery(request);
                case 15: // url-querystring
                    return GetQueryString(request);
                case 16: // header
                    return GetHeader(request, name);
                case 17: // content-type
                    return GetContentType(request);
                case 18: // body
                    return _requestLogger.GetBody(request);
                case 19: // body length
                    return GetBodyLength(request);
                case 20: // country
                    return GetCountryName();
                case 21: // country-iso2
                    return GetCountryIsoCode();
                case 22: // continent
                    return GetContinentName();
                case 23: // Ip from Cloudflare headers. The most reliable header for the real client IP. Set on every request.
                    return GetClientIpFromHeaders(request, "CF-Connecting-IP");
                case 24: // Ip from X-Forwarded-For header. Standard proxy header. Cloudflare appends the client IP to the list.
                    return GetClientIpFromHeaders(request, "X-Forwarded-For");
                case 25: // Ip from True-Client-IP header. The original visitor IP. Used when Cloudflare is configured to pass the real IP explicitly.
                    return GetClientIpFromHeaders(request, "True-Client-IP");
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Gets the country name from the GeoIP context.
        /// </summary>
        /// <returns>The country name, or empty string if unavailable.</returns>
        public string GetCountryName()
        {
            var gi = _httpContextAccessor.GetContextItem(GeoInfoContextKey) as CountryResponse;
            return gi?.Country?.Name ?? string.Empty;
        }

        /// <summary>
        /// Gets the two-letter ISO country code from the GeoIP context.
        /// </summary>
        /// <returns>The ISO country code, or empty string if unavailable.</returns>
        public string GetCountryIsoCode()
        {
            var gi = _httpContextAccessor.GetContextItem(GeoInfoContextKey) as CountryResponse;
            return gi?.Country?.IsoCode ?? string.Empty;
        }

        /// <summary>
        /// Gets the continent name from the GeoIP context.
        /// </summary>
        /// <returns>The continent name, or empty string if unavailable.</returns>
        public string GetContinentName()
        {
            var gi = _httpContextAccessor.GetContextItem(GeoInfoContextKey) as CountryResponse;
            return gi?.Continent?.Name ?? string.Empty;
        }

        /// <summary>
        /// Gets a cookie value from the HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="name">The cookie name.</param>
        /// <returns>The cookie value, or empty string if not found.</returns>
        public string GetCookieValue(HttpRequest request, string name)
            => request.Cookies[name]?.Value ?? "";

        /// <summary>
        /// Gets the hostname from the HTTP request URL.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The hostname.</returns>
        public string GetHostname(HttpRequest request)
            => request.Url.Host ?? "";

        /// <summary>
        /// Gets the client IP address, considering proxy headers when behind trusted proxies.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The client IP address.</returns>
        public string GetClientIp(HttpRequest request)
        {
            // Trust X-Forwarded-For only if behind trusted proxy
            var trustedProxies = _configuration.GetAppSetting("TrustedProxyIPs")?.Split(',') ?? new string[0];
            var directIp = request.UserHostAddress ?? "";

            if (trustedProxies.Contains(directIp))
            {
                var xForwardedFor = request.Headers["X-Forwarded-For"];
                if (!string.IsNullOrEmpty(xForwardedFor))
                {
                    // Take the first IP (original client)
                    return xForwardedFor.Split(',')[0].Trim();
                }
            }

            return directIp;
        }

        /// <summary>
        /// Gets the client IP address from Cloudflare headers, falling back to direct IP if not present.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="headerName"></param>
        /// <returns></returns>
        public string GetClientIpFromHeaders(HttpRequest request, string headerName)
            => request.Headers[headerName] ?? GetClientIp(request);

        /// <summary>
        /// Gets the protocol (http or https) from the HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The protocol string.</returns>
        public string GetProtocol(HttpRequest request)
            => request.IsSecureConnection ? "https" : "http";

        /// <summary>
        /// Gets the referrer URL from the HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The referrer URL, or empty string if not provided.</returns>
        public string GetReferrer(HttpRequest request)
            => request.UrlReferrer?.AbsoluteUri ?? string.Empty;

        /// <summary>
        /// Gets the HTTP method (GET, POST, etc.) from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The HTTP method.</returns>
        public string GetHttpMethod(HttpRequest request)
            => request.HttpMethod ?? "";

        /// <summary>
        /// Gets the HTTP version from the server variables.
        /// </summary>
        /// <returns>The HTTP version string.</returns>
        public string GetHttpVersion()
            => _httpContextAccessor.Current?.Request.ServerVariables["SERVER_PROTOCOL"] ?? "";

        /// <summary>
        /// Gets the user agent string from the HTTP request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The user agent string.</returns>
        public string GetUserAgent(HttpRequest request)
            => request.UserAgent ?? "";

        /// <summary>
        /// Gets the X-Forwarded-For header value.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The X-Forwarded-For value, or empty string if not present.</returns>
        public string GetXForwardedFor(HttpRequest request)
            => request.Headers["X-Forwarded-For"] ?? "";

        /// <summary>
        /// Gets the MIME type of the request from Content-Type header or file extension.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The MIME type string.</returns>
        public string GetMimeType(HttpRequest request)
        {
            // First, try to get from Content-Type header (for POST/PUT requests)
            var contentType = request.ContentType;
            if (!string.IsNullOrEmpty(contentType))
            {
                // Extract just the MIME type (remove charset and other parameters)
                var semicolonIndex = contentType.IndexOf(';');
                return semicolonIndex > 0
                    ? contentType.Substring(0, semicolonIndex).Trim().ToLower()
                    : contentType.Trim().ToLower();
            }

            // If no Content-Type header, try to determine from file extension
            var path = request.Url.AbsolutePath;
            var extension = System.IO.Path.GetExtension(path)?.ToLowerInvariant();

            if (string.IsNullOrEmpty(extension))
                return string.Empty;

            return MimeMapping.GetMimeMapping(path).ToLower();
        }

        /// <summary>
        /// Gets the full absolute URI of the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The full URL.</returns>
        public string GetFullUrl(HttpRequest request)
            => request.Url.AbsoluteUri;

        /// <summary>
        /// Gets the URL path (without query string) from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The URL path.</returns>
        public string GetUrlPath(HttpRequest request)
            => request.Url.AbsolutePath ?? "";

        /// <summary>
        /// Gets the URL path and query string from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The URL path and query string.</returns>
        public string GetUrlPathAndQuery(HttpRequest request)
            => request.Url.PathAndQuery ?? "";

        /// <summary>
        /// Gets the query string from the request URL.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The query string.</returns>
        public string GetQueryString(HttpRequest request)
            => request.Url.Query.ToString();

        /// <summary>
        /// Gets a specific HTTP header value from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="name">The header name.</param>
        /// <returns>The header value, or empty string if not found.</returns>
        public string GetHeader(HttpRequest request, string name)
            => request.Headers[name] ?? "";

        /// <summary>
        /// Gets the Content-Type header value from the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The content type string.</returns>
        public string GetContentType(HttpRequest request)
            => request.ContentType ?? "";

        /// <summary>
        /// Gets the length of the request body in bytes.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The body length as a string.</returns>
        public string GetBodyLength(HttpRequest request)
        {
            if (request?.InputStream != null)
            {
                return request.InputStream.Length.ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the version number of the executing assembly.
        /// </summary>
        /// <returns>The assembly version string, or a default version if unavailable.</returns>
        public string GetAssemblyVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString() : "2025.1.1.1";
        }

        /// <summary>
        /// Generates the HTML page for an interactive challenge (user must click checkbox).
        /// </summary>
        /// <param name="rootDomain">The domain name.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="csrfToken">The CSRF protection token.</param>
        /// <returns>The complete HTML page as a string.</returns>
        public string GenerateHTMLInteractiveChallenge(string rootDomain, string rayId, string csrfToken)
            => $@"<!DOCTYPE html>
        <html>
        <head>
            <title>Just a moment...</title>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <style>
                * {{box - sizing: border-box;}}
                body {{
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    height: 100vh;
                    margin: 0;
                    font-family: Arial, sans-serif;
                    background-color: #f9f9f9;
                }}
                .heading-favicon {{margin - right: .5rem;
                    width: 2rem;
                    height: 2rem;
                }}
                .container {{
                    text-align: center;
                    padding: 20px;
                    border: 1px solid #ccc;
                    border-radius: 8px;
                    background-color: #fff;
                    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                    display: none; /* Hide container by default */
                }}
                .checkbox-container {{
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    margin-top: 20px;
                    animation: fadeIn 1s ease;
                }}
                input[type='checkbox'] {{
                    margin-right: 10px;
                    transform: scale(1.5);
                    transition: transform 0.3s ease;
                }}
                input[type='checkbox']:hover {{
                    transform: scale(1.7);
                }}
                input[type='checkbox'].clicked {{
                    animation: pulse 0.5s ease;
                }}
                .form-processing {{
                    animation: fadeOut 1s ease forwards;
                }}
                .loader {{
                    display: none;
                    border: 4px solid #f3f3f3;
                    border-top: 4px solid #007bff;
                    border-radius: 50%;
                    width: 40px;
                    height: 40px;
                    animation: spin 1s linear infinite;
                    margin: 20px auto;
                }}
                .loader.active {{
                    display: block;
                }}
                .noscript-message, .nocookies-message {{
                    color: red;
                    font-size: 16px;
                    margin-top: 20px;
                }}
                .checkbox-box {{
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    padding: 15px;
                    border: 2px solid #ccc;
                    border-radius: 8px;
                    background-color: #f9f9f9;
                    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                    transition: box-shadow 0.3s ease, border-color 0.3s ease;
                }}
                .checkbox-box:hover {{
                    box-shadow: 0 6px 8px rgba(0, 0, 0, 0.15);
                    border-color: #007bff;
                }}
                .checkbox-box input[type='checkbox'] {{
                    margin-right: 10px;
                    transform: scale(1.5);
                    transition: transform 0.3s ease;
                }}
                .checkbox-box input[type='checkbox']:hover {{
                    transform: scale(1.7);
                }}
                .checkbox-box label {{
                    font-size: 16px;
                    font-weight: bold;
                    color: #333;
                }}
                @keyframes fadeIn {{
                    from {{ opacity: 0; transform: translateY(-20px); }}
                    to {{ opacity: 1; transform: translateY(0); }}
                }}
                @keyframes fadeOut {{
                    from {{ opacity: 1; transform: translateY(0); }}
                    to {{ opacity: 0; transform: translateY(-20px); }}
                }}
                @keyframes pulse {{
                    0% {{ transform: scale(1.5); }}
                    50% {{ transform: scale(1.8); }}
                    100% {{ transform: scale(1.5); }}
                }}
                @keyframes spin {{
                    0% {{ transform: rotate(0deg); }}
                    100% {{ transform: rotate(360deg); }}
                }}
                
                @media (max-width: 600px) {{
                    body {{
                        padding: 16px;
                        height: auto;
                        min-height: 100vh;
                    }}

                    .container {{
                        width: 100%;
                        max-width: 480px;
                    }}

                    .checkbox-box label {{
                        font-size: 14px;
                    }}

                    .loader {{
                        width: 32px;
                        height: 32px;
                    }}
                }}

                @media (max-width: 360px) {{
                    .checkbox-box {{
                        padding: 10px;
                    }}

                    .checkbox-box label {{
                        font-size: 13px;
                    }}
                }}
            </style>
            <script>
                // Check if cookies are enabled
                function checkCookies() {{
                    if (!navigator.cookieEnabled) {{
                        window.addEventListener('DOMContentLoaded', function () {{
                            var cookieEl = document.querySelector('.nocookies-message');
                            if (cookieEl) {{
                                cookieEl.style.display = 'block';
                            }}
                        }});
                        return false;
                    }}
                    return true;
                }}

                // Show the container if JavaScript and cookies are enabled
                function showContainerIfEnabled() {{
                    const container = document.querySelector('.container');
                    if (checkCookies()) {{
                        container.style.display = 'block';
                    }}
                }}

                // Show loader and hide checkbox-container for 3 seconds on page load
                function showLoaderOnPageLoad() {{
                    const loader = document.querySelector('.loader');
                    const checkboxContainer = document.querySelector('.checkbox-container');
                    const checkbox = document.querySelector('#verifyCheckbox');

                    checkbox.disabled = true; // Disable the checkbox
                    checkboxContainer.style.display = 'none'; // Hide the checkbox-container
                    loader.classList.add('active'); // Show the loader

                    setTimeout(() => {{
                        loader.classList.remove('active'); // Hide the loader
                        checkboxContainer.style.display = 'flex'; // Show the checkbox-container
                        checkbox.disabled = false; // Re-enable the checkbox
                    }}, 3000);
                }}

                function handleCheckboxChange(event) {{
                    const checkbox = event.target;
                    const form = checkbox.closest('form');
                    const loader = document.querySelector('.loader');
                    const checkboxContainer = document.querySelector('.checkbox-container');

                    // If the checkbox is already disabled, return early to prevent duplicate actions
                    if (checkbox.disabled) {{
                        return;
                    }}

                    // Disable the checkbox to prevent multiple triggers
                    checkbox.disabled = true;

                    // Add animation class to checkbox
                    checkbox.classList.add('clicked');

                    // Remove the animation class after it completes
                    setTimeout(() => {{
                        checkbox.classList.remove('clicked');
                    }}, 500);

                    // If the checkbox is checked, show the loader and process the form
                    if (checkbox.checked) {{
                        loader.classList.add('active');
                        checkboxContainer.style.display = 'none'; // Hide the checkbox-container
                        setTimeout(() => {{
                            form.submit(); // Submit the form after the loader animation
                        }}, 3000); // Wait for the loader to complete
                    }}
                }}

                // Attach event listener dynamically
                window.onload = () => {{
                    showContainerIfEnabled();
                    showLoaderOnPageLoad();

                    const checkbox = document.querySelector('#verifyCheckbox');
                    checkbox.addEventListener('change', handleCheckboxChange);
    
                    console.log(""%c ¡Espera!"", ""color: Red; font-size: 45px; font-weight: bold;"");
                    console.log(""%cEsta función del navegador está pensada para desarrolladores. Si alguien te indicó que copiaras y pegaras algo aquí para habilitar una función o para \""piratear\"" la cuenta de alguien, se trata de un fraude."", ""color: green; font-size: x-large;"");
                }};
            </script>
        </head>
        <body>
            <div class=""container"">
                <h1><img src=""/favicon.ico"" class=""heading-favicon"" alt=""Icon for {HttpUtility.HtmlEncode(rootDomain)}"">  
                    {HttpUtility.HtmlEncode(rootDomain)}</h1>
                <b><p>Verificando que eres humano. Esto puede durar unos segundos.</p></b>
                <form method=""post"" action="""">
                    <input type=""hidden"" name=""__rayId"" value=""{HttpUtility.HtmlEncode(rayId)}"" />
                    <input type=""hidden"" name=""__csrf"" value=""{HttpUtility.HtmlEncode(csrfToken)}"" />
                    <div class=""checkbox-container"">
                        <div class=""checkbox-box"">
                            <input type=""checkbox"" id=""verifyCheckbox"">
                            <label for=""verifyCheckbox"">No soy un robot</label>
                        </div>
                    </div>
                    <div class=""loader""></div>
                </form>
                <br/>
                <p>{HttpUtility.HtmlEncode(rootDomain)} necesita revisar la seguridad de la conexión antes de proceder.</p>
                <br/><br/><br/>
                <hr/>
                <em>Ray Id: {HttpUtility.HtmlEncode(rayId)}</em>
                <br/>   
                <em>Powered by {HttpUtility.HtmlEncode(rootDomain)} | Version: {HttpUtility.HtmlEncode(GetAssemblyVersion())}</em>
            </div>
            <noscript>
                <div class=""noscript-message"">
                    JavaScript is disabled in your browser. Please enable JavaScript to proceed.
                </div>
            </noscript>
            <div class=""nocookies-message"" style=""display: none; text-align: center;"">
                Cookies are disabled in your browser. Please enable cookies to proceed.
            </div>
        </body>
        </html>";

        /// <summary>
        /// Generates the HTML page for a managed challenge (automatic verification after delay).
        /// </summary>
        /// <param name="rootDomain">The domain name.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="csrfToken">The CSRF protection token.</param>
        /// <returns>The complete HTML page as a string.</returns>
        public string GenerateHTMLManagedChallenge(string rootDomain, string rayId, string csrfToken)
    => $@"<!DOCTYPE html>
<html>
<head>
    <title>Just a moment...</title>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <style>
        * {{box - sizing: border-box;}}
        body {{
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            font-family: Arial, sans-serif;
            background-color: #f9f9f9;
        }}
        .heading-favicon {{margin - right: .5rem;
            width: 2rem;
            height: 2rem;
        }}
        .container {{
            text-align: center;
            padding: 20px;
            border: 1px solid #ccc;
            border-radius: 8px;
            background-color: #fff;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            display: none; /* Hide container by default */
        }}
        .loader {{
            display: none;
            border: 4px solid #f3f3f3;
            border-top: 4px solid #007bff;
            border-radius: 50%;
            width: 40px;
            height: 40px;
            animation: spin 1s linear infinite;
            margin: 20px auto;
        }}
        .loader.active {{
            display: block;
        }}
        .noscript-message, .nocookies-message {{
            color: red;
            font-size: 16px;
            margin-top: 20px;
        }}
        @keyframes spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
        }}
        @media (max-width: 600px) {{
            body {{ padding: 16px; height: auto; min-height: 100vh; }}
            .container {{ width: 100%; max-width: 480px; }}
            .checkbox-box label {{ font-size: 14px; }}
            .loader {{ width: 32px; height: 32px; }}
        }}

        @media (max-width: 360px) {{
            .checkbox-box {{ padding: 10px; }}
            .checkbox-box label {{ font-size: 13px; }}
        }}
    </style>
    <script>
        function checkCookies() {{
                    if (!navigator.cookieEnabled) {{
                        window.addEventListener('DOMContentLoaded', function () {{
                            var cookieEl = document.querySelector('.nocookies-message');
                            if (cookieEl) {{
                                cookieEl.style.display = 'block';
                            }}
                        }});
                        return false;
                    }}
                    return true;
                }}
        function showContainerIfEnabled() {{
            const container = document.querySelector('.container');
            if (checkCookies()) {{
                container.style.display = 'block';
            }}
        }}

        function showLoaderOnPageLoad() {{
            const loader = document.querySelector('.loader');
            const form = document.querySelector('form');

            loader.classList.add('active'); // Show the loader

            setTimeout(() => {{
                loader.classList.remove('active'); // Hide the loader
                form.submit(); // Automatically submit the form
            }}, 3000);
        }}

        window.onload = () => {{
            showContainerIfEnabled();
            showLoaderOnPageLoad();
        }};
    </script>
</head>
<body>
    <div class=""container"">
        <h1><img src=""/favicon.ico"" class=""heading-favicon"" alt=""Icon for {HttpUtility.HtmlEncode(rootDomain)}"">
            {HttpUtility.HtmlEncode(rootDomain)}</h1>
        <b><p>Verificando que eres humano. Esto puede durar unos segundos.</p></b>
        <form method=""post"" action="""">
            <input type=""hidden"" name=""__rayId"" value=""{HttpUtility.HtmlEncode(rayId)}"" />
            <input type=""hidden"" name=""__csrf"" value=""{HttpUtility.HtmlEncode(csrfToken)}"" />
            <div class=""loader""></div>
        </form>
        <br/>
        <p>{HttpUtility.HtmlEncode(rootDomain)} necesita revisar la seguridad de la conexión antes de proceder.</p>
        <br/><br/><br/>
        <hr/>
        <em>Ray Id: {HttpUtility.HtmlEncode(rayId)}</em>
        <br/>   
        <em>Powered by {HttpUtility.HtmlEncode(rootDomain)} | Version: {HttpUtility.HtmlEncode(GetAssemblyVersion())}</em>
    </div>
    <noscript>
        <div class=""noscript-message"">
            JavaScript is disabled in your browser. Please enable JavaScript to proceed.
        </div>
    </noscript>
    <div class=""nocookies-message"" style=""display: none; text-align: center;"">
        Cookies are disabled in your browser. Please enable cookies to proceed.
    </div>
</body>
</html>";

        /// <summary>
        /// Generates the HTML page displayed when a request is blocked.
        /// </summary>
        /// <param name="rootDomain">The domain name.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <returns>The complete HTML access denied page as a string.</returns>
        public string GenerateHTMLUserBlockedPage(string rootDomain, string rayId)
            => $@"<!DOCTYPE html>
                <html>
                <head>
                    <title>Access Denied</title>
                    <meta charset=""UTF-8"" />
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                    <style>
                        * {{box-sizing: border-box;}}
                        body {{
                            display: flex;
                            justify-content: center;
                            align-items: center;
                            height: 100vh;
                            margin: 0;
                            font-family: Arial, sans-serif;
                            background-color: #f9f9f9;
                        }}
                        .heading-favicon {{margin - right: .5rem;
                            width: 2rem;
                            height: 2rem;
                        }}
                        .noscript-message, .nocookies-message {{
                            color: red;
                            font-size: 16px;
                            margin-top: 20px;
                        }}
                        .container {{
                            text-align: center;
                            padding: 20px;
                            border: 1px solid #ccc;
                            border-radius: 8px;
                            background-color: #fff;
                            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                            display: none; /* Hide container by default */
                        }}
                        h1 {{
                            color: #d9534f;
                        }}
                        p {{
                            font-size: 16px;
                            color: #333;
                        }}
                        .details {{
                            margin-top: 20px;
                            font-size: 14px;
                            color: #777;
                        }}
                        hr {{
                            margin: 20px 0;
                            border: none;
                            border-top: 1px solid #ddd;
                        }}
                         @media (max-width: 600px) {{
                                    body {{
                                        padding: 16px;
                                        height: auto;
                                        min-height: 100vh;
                                    }}

                                    .container {{
                                        width: 100%;
                                        max-width: 480px;
                                    }}

                                    .checkbox-box label {{
                                        font-size: 14px;
                                    }}

                                    .loader {{
                                        width: 32px;
                                        height: 32px;
                                    }}
                                }}

                                @media (max-width: 360px) {{
                                    .checkbox-box {{
                                        padding: 10px;
                                    }}

                                    .checkbox-box label {{
                                        font-size: 13px;
                                    }}
                                }}
                    </style>
                    <script>
                        // Check if cookies are enabled
                        function checkCookies() {{
                            if (!navigator.cookieEnabled) {{
                                window.addEventListener('DOMContentLoaded', function () {{
                                    var cookieEl = document.querySelector('.nocookies-message');
                                    if (cookieEl) {{
                                        cookieEl.style.display = 'block';
                                    }}
                                }});
                                return false;
                            }}
                            return true;
                        }}

                        // Show the container if JavaScript and cookies are enabled
                        function showContainerIfEnabled() {{
                            const container = document.querySelector('.container');
                            if (checkCookies()) {{
                                container.style.display = 'block';
                            }}
                        }}

                        // Show the container when JavaScript is enabled
                        window.onload = function() {{
                            showContainerIfEnabled();
                        }};
                    </script>
                </head>
                <body>
                    <div class=""container"">
                        <h1><img src=""/favicon.ico"" class=""heading-favicon"" alt=""Icon for {HttpUtility.HtmlEncode(rootDomain)}"">&nbsp;Access Denied</h1>
                        <p>Your request has been blocked by the server's security rules.</p>
                        <p>If you believe this is an error, please contact the website administrator.</p>
                        <div class=""details"">
                            <hr/>
                            <p><strong>Domain:</strong> {HttpUtility.HtmlEncode(rootDomain)}</p>
                            <p><strong>Ray Id:</strong> {HttpUtility.HtmlEncode(rayId)}</p>
                            <p><strong>Version:</strong> {HttpUtility.HtmlEncode(GetAssemblyVersion())}</p>
                            <hr/>
                        </div>
                    </div>
                    <noscript>
                        <div class=""noscript-message"">
                            JavaScript is disabled in your browser. Please enable JavaScript to proceed.
                        </div>
                    </noscript>
                    <div class=""nocookies-message"" style=""display: none; text-align: center;"">
                        Cookies are disabled in your browser. Please enable cookies to proceed.
                    </div>
                </body>
                </html>";

        /// <summary>
        /// Generates the HTML page displayed when rate limit is exceeded.
        /// </summary>
        /// <param name="rootDomain">The domain name.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <returns>The complete HTML rate limit page as a string.</returns>
        public string GenerateHTMLRateLimitPage(string rootDomain, string rayId)
            => $@"<!DOCTYPE html>
        <html>
        <head>
            <title>Too Many Requests</title>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <style>
                * {{box-sizing: border-box;}}
                body {{
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    height: 100vh;
                    margin: 0;
                    font-family: Arial, sans-serif;
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                }}
                .heading-favicon {{
                    margin-right: .5rem;
                    width: 2rem;
                    height: 2rem;
                }}
                .container {{
                    text-align: center;
                    padding: 40px;
                    border-radius: 12px;
                    background-color: #fff;
                    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
                    max-width: 500px;
                    display: none;
                }}
                .error-code {{
                    font-size: 72px;
                    font-weight: bold;
                    color: #ff6b6b;
                    margin: 0;
                    text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.1);
                }}
                h1 {{
                    color: #2c3e50;
                    margin: 20px 0 10px 0;
                    font-size: 24px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                }}
                .message {{
                    font-size: 16px;
                    color: #555;
                    line-height: 1.6;
                    margin: 20px 0;
                }}
                .retry-info {{
                    background-color: #f8f9fa;
                    padding: 20px;
                    border-radius: 8px;
                    margin: 20px 0;
                    border-left: 4px solid #667eea;
                }}
                .retry-info strong {{
                    color: #667eea;
                    font-size: 18px;
                }}
                .countdown {{
                    font-size: 32px;
                    font-weight: bold;
                    color: #667eea;
                    margin: 10px 0;
                }}
                .progress-bar {{
                    width: 100%;
                    height: 8px;
                    background-color: #e0e0e0;
                    border-radius: 4px;
                    overflow: hidden;
                    margin-top: 10px;
                }}
                .progress-fill {{
                    height: 100%;
                    background: linear-gradient(90deg, #667eea 0%, #764ba2 100%);
                    width: 100%;
                    animation: countdown 60s linear;
                }}
                @keyframes countdown {{
                    from {{ width: 100%; }}
                    to {{ width: 0%; }}
                }}
                .retry-button {{
                    display: inline-block;
                    margin-top: 20px;
                    padding: 12px 30px;
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                    color: white;
                    text-decoration: none;
                    border-radius: 6px;
                    font-weight: bold;
                    transition: transform 0.2s, box-shadow 0.2s;
                    cursor: pointer;
                    border: none;
                    font-size: 16px;
                }}
                .retry-button:hover {{
                    transform: translateY(-2px);
                    box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
                }}
                .retry-button:disabled {{
                    opacity: 0.5;
                    cursor: not-allowed;
                    transform: none;
                }}
                .noscript-message, .nocookies-message {{
                    color: #d9534f;
                    font-size: 16px;
                    margin-top: 20px;
                    padding: 15px;
                    background-color: #fee;
                    border-radius: 6px;
                }}
                @media (max-width: 600px) {{
                    body {{
                        padding: 16px;
                        height: auto;
                        min-height: 100vh;
                    }}
                    .container {{
                        width: 100%;
                        max-width: 100%;
                        padding: 30px 20px;
                    }}
                    .error-code {{
                        font-size: 56px;
                    }}
                    h1 {{
                        font-size: 20px;
                    }}
                    .message {{
                        font-size: 14px;
                    }}
                    .countdown {{
                        font-size: 24px;
                    }}
                }}
                @media (max-width: 360px) {{
                    .container {{
                        padding: 20px 15px;
                    }}
                    .error-code {{
                        font-size: 48px;
                    }}
                    .retry-info {{
                        padding: 15px;
                    }}
                }}
            </style>
            <script>
                let countdownSeconds = 60;
                let retryButton;

                function checkCookies() {{
                    if (!navigator.cookieEnabled) {{
                        window.addEventListener('DOMContentLoaded', function () {{
                            var cookieEl = document.querySelector('.nocookies-message');
                            if (cookieEl) {{
                                cookieEl.style.display = 'block';
                            }}
                        }});
                        return false;
                    }}
                    return true;
                }}

                function showContainerIfEnabled() {{
                    const container = document.querySelector('.container');
                    if (checkCookies()) {{
                        container.style.display = 'block';
                    }}
                }}

                function startCountdown() {{
                    const countdownEl = document.querySelector('.countdown');
                    retryButton = document.querySelector('.retry-button');
            
                    const interval = setInterval(() => {{
                        countdownSeconds--;
                        countdownEl.textContent = countdownSeconds + 's';
                
                        if (countdownSeconds <= 0) {{
                            clearInterval(interval);
                            countdownEl.textContent = 'Ready!';
                            retryButton.disabled = false;
                            retryButton.textContent = 'Retry Now';
                        }}
                    }}, 1000);
                }}

                function retryRequest() {{
                    if (countdownSeconds <= 0) {{
                        window.location.reload();
                    }}
                }}

                window.onload = function() {{
                    showContainerIfEnabled();
                    startCountdown();
            
                    console.log(""%c ¡Espera!"", ""color: Red; font-size: 45px; font-weight: bold;"");
                    console.log(""%cEsta función del navegador está pensada para desarrolladores. Si alguien te indicó que copiaras y pegaras algo aquí para habilitar una función o para \""piratear\"" la cuenta de alguien, se trata de un fraude."", ""color: green; font-size: x-large;"");
                }};
            </script>
        </head>
        <body>
            <div class=""container"">
                <div class=""icon"">⏱️</div>
                <p class=""error-code"">429</p>
                <h1>
                    <img src=""/favicon.ico"" class=""heading-favicon"" alt=""Icon for {HttpUtility.HtmlEncode(rootDomain)}"">
                    Too Many Requests
                </h1>
                <p class=""message"">
                    You've made too many requests to <strong>{HttpUtility.HtmlEncode(rootDomain)}</strong> in a short period of time.
                    Please wait a moment before trying again.
                </p>
        
                <div class=""retry-info"">
                    <strong>Please wait</strong>
                    <div class=""countdown"">60s</div>
                    <div class=""progress-bar"">
                        <div class=""progress-fill""></div>
                    </div>
                </div>

                <button class=""retry-button"" onclick=""retryRequest()"" disabled>
                    Please wait...
                </button>

                <p class=""message"" style=""margin-top:  20px; font-size: 14px; color: #777;"">
                    This protection helps keep {HttpUtility.HtmlEncode(rootDomain)} safe and performing well for everyone.
                </p>

                <div class=""details"">
                    <p><strong>Domain:</strong> {HttpUtility.HtmlEncode(rootDomain)}</p>
                    <p><strong>Ray Id:</strong> {HttpUtility.HtmlEncode(rayId)}</p>
                    <p><strong>Error Code:</strong> HTTP 429 - Rate Limit Exceeded</p>
                    <p><strong>Version:</strong> {HttpUtility.HtmlEncode(GetAssemblyVersion())}</p>
                </div>
            </div>
    
            <noscript>
                <div class=""noscript-message"" style=""text-align: center; max-width: 500px; margin: 0 auto;"">
                    <strong>JavaScript is disabled in your browser.</strong><br>
                    Please enable JavaScript or wait 60 seconds before refreshing the page.
                </div>
            </noscript>
            <div class=""nocookies-message"" style=""display: none; text-align: center; max-width: 500px; margin: 0 auto;"">
                <strong>Cookies are disabled in your browser.</strong><br>
                Please enable cookies to proceed.
            </div>
        </body>
        </html>";

        /// <summary>
        /// Adds a clearance token to the cache with the specified expiration time.
        /// </summary>
        /// <param name="token">The clearance token.</param>
        /// <param name="expirationTime">The token expiration time in UTC.</param>
        public void AddTokenToCache(string token, DateTime expirationTime)
        {
            try
            {
                // Cache absolute expiration expects local time
                var absoluteExpirationLocal = expirationTime.ToLocalTime();
                _tokenCache.Insert(token, expirationTime, null, absoluteExpirationLocal, Cache.NoSlidingExpiration);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"AddTokenToCache failed: {ex}");
                // If caching fails for any reason, fall back to storing in Application to avoid breaking behavior
                try
                {
                    var current = _httpContextAccessor.Current;
                    if (current != null)
                    {
                        current.Application[token] = expirationTime;
                    }
                }
                catch (Exception inner)
                {
                    Trace.TraceError($"Fallback AddTokenToCache (Application) failed: {inner}");
                }
            }
        }

        /// <summary>
        /// Called at the end of request processing to log the response details.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        public void Context_EndRequest(object sender, EventArgs e)
        {
            try
            {
                var app = (HttpApplication)sender;

                if (app.Context.Items["RequestStartTime"] is Stopwatch stopwatch)
                {
                    stopwatch.Stop();

                    // Fire-and-forget logging
                    var cs = GetConnectionString(app.Context.Request);
                    Guid ray = app.Context.Items["RayId"] is string r ? Guid.Parse(r) : Guid.Empty;
                    _requestLogger.EnqueueResponse(new LogEntrySafeResponse()
                    {
                        RayId = ray,
                        Url = app.Context.Request.Url?.ToString(),
                        HttpMethod = app.Context.Request.HttpMethod,
                        ResponseTime = stopwatch.ElapsedMilliseconds,
                        Timestamp = DateTime.UtcNow,
                        StatusCode = 200
                    }, cs);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"FrontGuardModule.Context_EndRequest error: {ex}");
            }
        }

        /// <summary>
        /// Prepares the response by removing unnecessary headers and adding security headers.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        public void Context_PreSendRequestHeaders(object sender, EventArgs e)
        {
            try
            {
                var app = (HttpApplication)sender;
                var response = app.Context.Response;

                RemoveUnnecessaryHeaders(response);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"FrontGuardModule.Context_PreSendRequestHeaders error: {ex}");
            }
        }

        /// <summary>
        /// Removes unnecessary server information headers from the HTTP response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        public void RemoveUnnecessaryHeaders(HttpResponse response)
        {
            try
            {
                response.Headers.Remove("X-Powered-By");
                response.Headers.Set("X-Powered-By", "IISFrontGuard");

                response.Headers.Remove("Server");
                response.Headers.Set("Server", "IISFrontGuard");

                response.Headers.Remove("X-AspNet-Version");
                response.Headers.Remove("X-AspNetMvc-Version");
            }
            catch
            {
                // Shallow
            }
        }

        /// <summary>
        /// Gets the database connection string for the specified request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The connection string.</returns>
        public string GetConnectionString(HttpRequest request)
            => GetConnectionStringByHost(request?.Url?.Host ?? string.Empty);

        /// <summary>
        /// Gets the database connection string for the specified host.
        /// Checks for host-specific configuration, then default configuration, then falls back to hardcoded string.
        /// </summary>
        /// <param name="host">The hostname.</param>
        /// <returns>The connection string.</returns>
        public string GetConnectionStringByHost(string host)
        {
            try
            {
                if (!string.IsNullOrEmpty(host))
                {
                    var hostConnectionString = GetHostSpecificConnectionString(host);
                    if (hostConnectionString != null)
                    {
                        return hostConnectionString;
                    }
                }

                var defaultName = _configuration.GetAppSetting("IISFrontGuard.DefaultConnectionStringName");
                if (!string.IsNullOrEmpty(defaultName))
                {
                    var defaultCs = _configuration.GetConnectionString(defaultName);
                    if (defaultCs != null)
                    {
                        return defaultCs;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"GetConnectionStringByHost encountered an error: {ex}");
            }

            return _fallbackConnectionString;
        }

        /// <summary>
        /// Gets a host-specific connection string from configuration.
        /// </summary>
        /// <param name="host">The hostname to look up.</param>
        /// <returns>The host-specific connection string, or null if not configured.</returns>
        public string GetHostSpecificConnectionString(string host)
        {
            var hostKey = $"GlobalLogger.Host.{host}";
            var hostValue = _configuration.GetAppSetting(hostKey);

            if (string.IsNullOrEmpty(hostValue))
            {
                return null;
            }

            var named = _configuration.GetConnectionString(hostValue);
            if (named != null)
            {
                return named;
            }

            return hostValue;
        }

        /// <summary>
        /// Sends a security event notification via webhook.
        /// </summary>
        /// <param name="securityEvent">The security event to send.</param>
        public void SendSecurityEventNotification(SecurityEvent securityEvent)
            => _webhookNotifier.EnqueueSecurityEvent(securityEvent);

        /// <summary>
        /// Determines the severity level based on the WAF rule's priority.
        /// Lower priority numbers (1-10) are critical, higher numbers are less severe.
        /// </summary>
        /// <param name="rule">The WAF rule.</param>
        /// <returns>The severity level string (Critical, High, Medium, or Low).</returns>
        public string DetermineSeverityFromRule(WafRule rule)
        {
            if (rule.Prioridad <= 10)
            {
                return SecurityEventSeverity.Critical;
            }

            if (rule.Prioridad <= 50)
            {
                return SecurityEventSeverity.High;
            }

            if (rule.Prioridad <= 100)
            {
                return SecurityEventSeverity.Medium;
            }

            return SecurityEventSeverity.Low;
        }

        /// <summary>
        /// Tracks challenge failures for a client IP to detect brute force attempts.
        /// Sends notification if failures exceed threshold.
        /// </summary>
        /// <param name="clientIp">The client IP address.</param>
        /// <param name="rayId">The unique request identifier.</param>
        /// <param name="reason">The failure reason.</param>
        public void TrackChallengeFailure(string clientIp, string rayId, string reason)
        {
            var info = _challengeFailures.GetOrAdd(clientIp, _ => new ChallengeFailureInfo
            {
                FirstFailure = DateTime.UtcNow,
                FailureCount = 0
            });

            lock (info)
            {
                if ((DateTime.UtcNow - info.FirstFailure).TotalMinutes > 10)
                {
                    info.FailureCount = 1;
                    info.FirstFailure = DateTime.UtcNow;
                }
                else
                {
                    info.FailureCount++;

                    if (info.FailureCount >= 3 && webhookEnabled)
                    {
                        SendSecurityEventNotification(new SecurityEvent
                        {
                            EventType = SecurityEventTypes.MultipleChallengeFails,
                            Severity = SecurityEventSeverity.High,
                            Timestamp = DateTime.UtcNow,
                            RayId = rayId,
                            ClientIp = clientIp,
                            Description = $"Multiple challenge failures detected: {info.FailureCount} attempts in {(DateTime.UtcNow - info.FirstFailure).TotalMinutes:F2} minutes. Reason: {reason}",
                            AdditionalData = new { failure_count = info.FailureCount, reason }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Disposes resources used by the module, stopping background services.
        /// </summary>
        public void Dispose()
        {
            _webhookNotifier.Stop();
            _requestLogger.Stop();
        }
    }
}