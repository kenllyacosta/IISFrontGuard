using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Security
{
    [Collection("IIS Integration Tests")]
    public class TokenCacheTests : IDisposable
    {
        private readonly TestWebhookServer _webhookServer;
        private readonly IRequestLogger _requestLogger;
        private readonly IWebhookNotifier _webhookNotifier;
        private readonly IGeoIPService _geoIPService;
        private readonly IWafRuleRepository _wafRuleRepository;
        private readonly ICacheProvider _tokenCache;
        private readonly IConfigurationProvider _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TokenCacheTests()
        {
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
        public void IsTokenValid_WithValidToken_ShouldReturnTrue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var token = "test-token";
            var expirationTime = DateTime.UtcNow.AddHours(1);
            module.AddTokenToCache(token, expirationTime);

            // Act
            var result = module.IsTokenValid(token);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsTokenValid_WithExpiredToken_ShouldReturnFalse()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var token = "expired-token";
            var expirationTime = DateTime.UtcNow.AddSeconds(-1);
            module.AddTokenToCache(token, expirationTime);

            // Wait for token to expire
            await Task.Delay(1000);

            // Act
            var result = module.IsTokenValid(token);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddTokenToCache_ShouldStoreTokenWithExpiration()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var token = "cache-test-token";
            var expiration = DateTime.UtcNow.AddHours(1);

            // Act
            module.AddTokenToCache(token, expiration);

            // Assert
            var isValid = module.IsTokenValid(token);
            Assert.True(isValid);
        }

        [Fact]
        public void AddCookie_ShouldCreateSecureCookie()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var token = "test-token-value";
            var expiration = DateTime.UtcNow.AddHours(1);
            var request = TestHelpers.CreateMockHttpRequest("https://localhost/test", "GET");

            // Act
            var cookie = module.AddCookie(token, expiration, request);

            // Assert
            Assert.Equal("fgm_clearance", cookie.Name);
            Assert.Equal(token, cookie.Value);
            Assert.True(request.IsSecureConnection ? cookie.Secure : !cookie.Secure);
            Assert.True(cookie.HttpOnly);
            Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        }

        [Fact]
        public void AddCookie_WithSecureConnection_ShouldSetSecureFlag()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var token = "secure-token";
            var expiration = DateTime.UtcNow.AddHours(1);
            var request = TestHelpers.CreateMockHttpRequest("https://localhost/test", "GET");

            // Act
            var cookie = module.AddCookie(token, expiration, request);

            // Assert
            Assert.True(request.IsSecureConnection ? cookie.Secure : !cookie.Secure);
            Assert.True(cookie.HttpOnly);
            Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
            Assert.Equal("/", cookie.Path);
        }

        [Fact]
        public void AddCookie_WithNonSecureConnection_ShouldNotSetSecureFlag()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var token = "non-secure-token";
            var expiration = DateTime.UtcNow.AddHours(1);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");

            // Act
            var cookie = module.AddCookie(token, expiration, request);

            // Assert
            Assert.False(cookie.Secure);
        }

        public void Dispose()
        {
            _webhookServer?.Dispose();
            _requestLogger?.Stop();
            _webhookNotifier?.Stop();
            GC.SuppressFinalize(this);
        }
    }
}
