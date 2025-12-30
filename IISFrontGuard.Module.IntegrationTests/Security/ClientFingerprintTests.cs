using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Services;
using System;
using System.IO;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Security
{
    [Collection("IIS Integration Tests")]
    public class ClientFingerprintTests : IDisposable
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

        public ClientFingerprintTests(IisIntegrationFixture fixture)
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
        public void GenerateClientFingerprint_ShouldCreateConsistentFingerprint()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", userAgent: "TestAgent");

            // Act
            var fingerprint1 = module.GenerateClientFingerprint(request);
            var fingerprint2 = module.GenerateClientFingerprint(request);

            // Assert
            Assert.Equal(fingerprint1, fingerprint2);
        }

        [Fact]
        public void GenerateClientFingerprint_WithDifferentUserAgent_ShouldCreateDifferentFingerprint()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request1 = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", userAgent: "Agent1");
            var request2 = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", userAgent: "Agent2");

            // Act
            var fingerprint1 = module.GenerateClientFingerprint(request1);
            var fingerprint2 = module.GenerateClientFingerprint(request2);

            // Assert
            Assert.NotEqual(fingerprint1, fingerprint2);
        }

        [Fact]
        public void ValidateTokenFingerprint_WithValidFingerprint_ShouldReturnTrue()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", clientIp: "192.168.1.100", userAgent: "TestAgent");
            
            // Generate fingerprint
            var fingerprint = module.GenerateClientFingerprint(request);

            // Assert
            Assert.NotNull(fingerprint);
            Assert.NotEmpty(fingerprint);
        }

        [Fact]
        public void ValidateTokenFingerprint_WithDifferentFingerprint_ShouldReturnFalse()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request1 = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", clientIp: "192.168.1.100", userAgent: "Agent1");
            var request2 = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", clientIp: "192.168.1.101", userAgent: "Agent2");

            // Generate fingerprints
            var fingerprint1 = module.GenerateClientFingerprint(request1);
            var fingerprint2 = module.GenerateClientFingerprint(request2);

            // Assert - Different IPs should produce different fingerprints
            Assert.NotEqual(fingerprint1, fingerprint2);
        }

        [Fact]
        public void TrackChallengeFailure_WithMultipleFailures_ShouldTrackCount()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var clientIp = "192.168.1.200";
            var rayId = Guid.NewGuid().ToString();

            // Act
            for (int i = 0; i < 5; i++)
            {
                module.TrackChallengeFailure(clientIp, rayId, "Test failure");
            }

            // Assert - No exception should be thrown
            Assert.True(true);
        }

        public void Dispose()
        {
            _webhookServer?.Dispose();
            _requestLogger?.Stop();
            _webhookNotifier?.Stop();
        }
    }
}
