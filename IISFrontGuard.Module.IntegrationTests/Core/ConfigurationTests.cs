using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Services;
using System;
using System.IO;
using System.Web;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Core
{
    [Collection("IIS Integration Tests")]
    public class ConfigurationTests : IDisposable
    {
        private readonly IisIntegrationFixture _fixture;
        private readonly string _testConnectionString;
        private readonly TestWebhookServer _webhookServer;
        private readonly IRequestLogger _requestLogger;
        private readonly IWebhookNotifier _webhookNotifier;
        private readonly IGeoIPService _geoIPService;
        private readonly IWafRuleRepository _wafRuleRepository;
        private readonly ICacheProvider _tokenCache;
        private readonly IConfigurationProvider _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ConfigurationTests(IisIntegrationFixture fixture)
        {
            _fixture = fixture;
            _testConnectionString = fixture.LocalDbAppCs;
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
        public void GetAppSettingAsInt_ShouldReturnConfiguredValue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            TestConfig.SetAppSetting("TestIntSetting", "42");

            // Act
            var result = module.GetAppSettingAsInt("TestIntSetting", 0);

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void GetAppSettingAsInt_WhenNotFound_ShouldReturnDefault()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);

            // Act
            var result = module.GetAppSettingAsInt("NonExistentSetting", 99);

            // Assert
            Assert.Equal(99, result);
        }

        [Fact]
        public void GetConnectionStringByHost_WithConfiguredHost_ShouldReturnHostSpecificString()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var host = "testhost.com";
            TestConfig.SetAppSetting($"GlobalLogger.Host.{host}", _testConnectionString);

            // Act
            var result = module.GetConnectionStringByHost(host);

            // Assert
            Assert.Equal(_testConnectionString, result);
        }

        [Fact]
        public void GetConnectionStringByHost_WithoutConfiguration_ShouldReturnFallback()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var host = "unconfigured-host.com";

            // Act
            var result = module.GetConnectionStringByHost(host);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("IISFrontGuard", result);
        }

        [Fact]
        public void GetHostSpecificConnectionString_WithConfiguredHost_ShouldReturnConnectionString()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var host = "specific-host.com";
            var expectedCs = "Server=localhost;Database=SpecificDb;";
            TestConfig.SetAppSetting($"GlobalLogger.Host.{host}", expectedCs);

            // Act
            var result = module.GetHostSpecificConnectionString(host);

            // Assert
            Assert.Equal(expectedCs, result);
        }

        [Fact]
        public void GetHostSpecificConnectionString_WithoutConfiguration_ShouldReturnNull()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var host = "non-configured-host.com";

            // Act
            var result = module.GetHostSpecificConnectionString(host);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetConnectionString_WithNullRequest_ShouldReturnFallback()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            HttpRequest nullRequest = null;

            // Act
            var result = module.GetConnectionString(nullRequest);

            // Assert - Should return fallback connection string
            Assert.NotNull(result);
            Assert.Contains("IISFrontGuard", result);
        }

        public void Dispose()
        {
            _webhookServer?.Dispose();
            _requestLogger?.Stop();
            _webhookNotifier?.Stop();
        }
    }
}
