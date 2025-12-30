using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Services;
using System;
using System.IO;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Pages
{
    [Collection("IIS Integration Tests")]
    public class ChallengePageTests : IDisposable
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

        public ChallengePageTests(IisIntegrationFixture fixture)
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
        public void GenerateHTMLInteractiveChallenge_ShouldContainRequiredElements()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var domain = "test.com";
            var rayId = Guid.NewGuid().ToString();
            var csrfToken = "test-csrf-token";

            // Act
            var html = module.GenerateHTMLInteractiveChallenge(domain, rayId, csrfToken);

            // Assert
            Assert.Contains(domain, html);
            Assert.Contains(rayId, html);
            Assert.Contains(csrfToken, html);
            Assert.Contains("checkbox", html);
            Assert.Contains("verifyCheckbox", html);
        }

        [Fact]
        public void GenerateHTMLManagedChallenge_ShouldContainRequiredElements()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var domain = "test.com";
            var rayId = Guid.NewGuid().ToString();
            var csrfToken = "test-csrf-token";

            // Act
            var html = module.GenerateHTMLManagedChallenge(domain, rayId, csrfToken);

            // Assert
            Assert.Contains(domain, html);
            Assert.Contains(rayId, html);
            Assert.Contains(csrfToken, html);
            Assert.Contains("loader", html);
        }

        [Fact]
        public void GenerateHTMLUserBlockedPage_ShouldContainRequiredElements()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var domain = "test.com";
            var rayId = Guid.NewGuid().ToString();

            // Act
            var html = module.GenerateHTMLUserBlockedPage(domain, rayId);

            // Assert
            Assert.Contains(domain, html);
            Assert.Contains(rayId, html);
            Assert.Contains("Access Denied", html);
            Assert.Contains("Ray Id", html);
        }

        [Fact]
        public void GenerateHTMLRateLimitPage_ShouldContainRequiredElements()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var domain = "test.com";
            var rayId = Guid.NewGuid().ToString();

            // Act
            var html = module.GenerateHTMLRateLimitPage(domain, rayId);

            // Assert
            Assert.Contains(domain, html);
            Assert.Contains(rayId, html);
            Assert.Contains("429", html);
            Assert.Contains("Too Many Requests", html);
            Assert.Contains("countdown", html);
        }

        public void Dispose()
        {
            _webhookServer?.Dispose();
            _requestLogger?.Stop();
            _webhookNotifier?.Stop();
        }
    }
}
