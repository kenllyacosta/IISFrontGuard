using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Helpers
{
    [Collection("IIS Integration Tests")]
    public class FieldValueExtractionTests : IDisposable
    {
        private readonly IisIntegrationFixture _fixture;
        private readonly TestWebhookServer _webhookServer;
        private readonly IRequestLogger _requestLogger;
        private readonly IWebhookNotifier _webhookNotifier;
        private readonly IGeoIPService _geoIPService;
        private readonly IWafRuleRepository _wafRuleRepository;
        private readonly ICacheProvider _tokenCache;
        private readonly IConfigurationProvider _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FieldValueExtractionTests(IisIntegrationFixture fixture)
        {
            _fixture = fixture;
            _webhookServer = new TestWebhookServer(9876);
            _webhookServer.Start();

            _requestLogger = new RequestLoggerAdapter();
            _webhookNotifier = new WebhookNotifierAdapter();
            
            var geoDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeoLite2-Country.mmdb");
            _geoIPService = new GeoIPServiceAdapter(geoDbPath);
            
            _tokenCache = new HttpRuntimeCacheProvider();
            _wafRuleRepository = new WafRuleRepository(_tokenCache);
            _configuration = new TestConfigurationProvider();
            _httpContextAccessor = new HttpContextAccessor();

            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Enabled", "true");
            TestConfig.SetAppSetting("IISFrontGuard.Webhook.Url", "http://localhost:9876/webhook");
            TestConfig.SetAppSetting("IISFrontGuardEncryptionKey", "TestKey123456789");
        }

        [Fact]
        public void GetFieldValue_ForHostname_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://testhost.com/path", "GET");

            // Act
            var result = module.GetFieldValue(2, request); // 2 = hostname

            // Assert
            Assert.Equal("testhost.com", result);
        }

        [Fact]
        public void GetFieldValue_ForProtocol_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://testhost.com/path", "GET");

            // Act
            var result = module.GetFieldValue(5, request); // 5 = protocol

            // Assert
            Assert.Equal("http", result);
        }

        [Fact]
        public void GetFieldValue_ForHttpMethod_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");

            // Act
            var result = module.GetFieldValue(7, request); // 7 = method

            // Assert
            Assert.Equal("GET", result);
        }

        [Fact]
        public void GetFieldValue_ForCookie_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            request.Cookies.Add(new HttpCookie("testCookie", "testValue"));

            // Act
            var result = module.GetFieldValue(1, request, "testCookie"); // 1 = cookie

            // Assert
            Assert.Equal("testValue", result);
        }

        [Fact]
        public void GetFieldValue_ForUserAgent_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", userAgent: "Mozilla/5.0");

            // Act
            var result = module.GetFieldValue(9, request); // 9 = user-agent

            // Assert
            Assert.Equal("Mozilla/5.0", result);
        }

        [Fact]
        public void GetFieldValue_ForFullUrl_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://testhost.com/path?query=value", "GET");

            // Act
            var result = module.GetFieldValue(12, request); // 12 = url-full

            // Assert
            Assert.Equal("http://testhost.com/path?query=value", result);
        }

        [Fact]
        public void GetFieldValue_ForUrlPathAndQuery_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/path?query=value", "GET");

            // Act
            var result = module.GetFieldValue(14, request); // 14 = url-path

            // Assert
            Assert.Contains("/path", result);
        }

        [Fact]
        public void GetFieldValue_ForQueryString_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test?param1=value1&param2=value2", "GET");

            // Act
            var result = module.GetFieldValue(15, request); // 15 = url-querystring

            // Assert
            Assert.Contains("param1=value1", result);
        }

        [Fact]
        public void GetFieldValue_ForHeader_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequestWithHeaders("http://localhost/test", "GET",
                headers: new Dictionary<string, string> { { "X-Custom-Header", "CustomValue" } });

            // Act
            var result = module.GetFieldValue(16, request, "X-Custom-Header"); // 16 = header

            // Assert
            Assert.Equal("CustomValue", result);
        }

        [Fact]
        public void GetFieldValue_ForContentType_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "POST");
            request.ContentType = "application/json";

            // Act
            var result = module.GetFieldValue(17, request); // 17 = content-type

            // Assert
            Assert.Equal("application/json", result);
        }

        [Fact]
        public void GetFieldValue_ForMimeType_ShouldReturnCorrectValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test.html", "GET");

            // Act
            var result = module.GetFieldValue(11, request); // 11 = mimetype

            // Assert
            Assert.Contains("html", result.ToLower());
        }

        [Fact]
        public void GetClientIp_WithDirectConnection_ShouldReturnUserHostAddress()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", clientIp: "192.168.1.50");

            // Act
            var result = module.GetClientIp(request);

            // Assert
            Assert.Equal("192.168.1.50", result);
        }

        [Fact]
        public void GetClientIp_WithXForwardedFor_AndTrustedProxy_ShouldReturnForwardedIp()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            TestConfig.SetAppSetting("TrustedProxyIPs", "127.0.0.1");
            var request = TestHelpers.CreateMockHttpRequestWithHeaders("http://localhost/test", "GET", clientIp: "127.0.0.1", 
                headers: new Dictionary<string, string> { { "X-Forwarded-For", "192.168.1.50, 10.0.0.1" } });

            // Act
            var result = module.GetClientIp(request);

            // Assert
            Assert.Equal("192.168.1.50", result);
        }

        [Fact]
        public void GetClientIp_WithCFConnectingIP_ShouldReturnCFIp()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequestWithHeaders("http://localhost/test", "GET", clientIp: "127.0.0.1",
                headers: new Dictionary<string, string> { { "CF-Connecting-IP", "203.0.113.1" } });

            // Act
            var result = module.GetClientIpFromHeaders(request, "CF-Connecting-IP");

            // Assert
            Assert.Equal("203.0.113.1", result);
        }

        [Fact]
        public void GetClientIp_WithTrueClientIP_ShouldReturnTrueClientIp()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequestWithHeaders("http://localhost/test", "GET", clientIp: "127.0.0.1",
                headers: new Dictionary<string, string> { { "True-Client-IP", "198.51.100.1" } });

            // Act
            var result = module.GetClientIpFromHeaders(request, "True-Client-IP");

            // Assert
            Assert.Equal("198.51.100.1", result);
        }

        [Fact]
        public void GetProtocol_WithHttpsRequest_ShouldReturnHttps()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");

            // Act
            var result = module.GetProtocol(request);

            // Assert
            Assert.Equal("http", result);
        }

        [Fact]
        public void GetReferrer_WithReferrerSet_ShouldReturnReferrer()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");

            // Act
            var result = module.GetReferrer(request);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        public void Dispose()
        {
            _webhookServer?.Dispose();
            _requestLogger?.Stop();
            _webhookNotifier?.Stop();
        }
    }
}
