using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Services;
using System;
using System.IO;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Helpers
{
    [Collection("IIS Integration Tests")]
    public class UtilityMethodsTests : IDisposable
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

        public UtilityMethodsTests(IisIntegrationFixture fixture)
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
        public void GetAssemblyVersion_ShouldReturnValidVersion()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);

            // Act
            var result = module.GetAssemblyVersion();

            // Assert
            Assert.NotNull(result);
            Assert.Matches(@"\d+\.\d+\.\d+\.\d+", result);
        }

        public void Dispose()
        {
            _webhookServer?.Dispose();
            _requestLogger?.Stop();
            _webhookNotifier?.Stop();
        }
    }
}
