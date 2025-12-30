using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using System;
using System.IO;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Notifications
{
    [Collection("IIS Integration Tests")]
    public class EventNotificationTests : IDisposable
    {
        private readonly TestWebhookServer _webhookServer;
        private readonly IRequestLogger _requestLogger;
        private readonly IWebhookNotifier _webhookNotifier;
        private readonly IGeoIPService _geoIPService;
        private readonly IWafRuleRepository _wafRuleRepository;
        private readonly ICacheProvider _tokenCache;
        private readonly IConfigurationProvider _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EventNotificationTests()
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
        public void CreateBlockedEventNotification_ShouldCreateValidEvent()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var rule = new WafRule
            {
                Id = 1,
                Nombre = "Test Rule",
                Prioridad = 10
            };
            var rayId = Guid.NewGuid().ToString();

            // Act
            var result = module.CreateBlockedEventNotification(request, rule, rayId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(SecurityEventTypes.RequestBlocked, result.EventType);
            Assert.Equal(rayId, result.RayId);
            Assert.Equal(rule.Id, result.RuleId);
            Assert.Equal(rule.Nombre, result.RuleName);
        }

        [Fact]
        public void CreateChallengeEventNotification_ShouldCreateValidEvent()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var rule = new WafRule
            {
                Id = 2,
                Nombre = "Challenge Rule",
                Prioridad = 20
            };
            var rayId = Guid.NewGuid().ToString();

            // Act
            var result = module.CreateChallengeEventNotification(request, rule, rayId, "managed");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(SecurityEventTypes.ChallengeIssued, result.EventType);
            Assert.Equal(rayId, result.RayId);
            Assert.Equal(rule.Id, result.RuleId);
            Assert.Contains("Managed challenge", result.Description);
        }

        public void Dispose()
        {
            _webhookServer?.Dispose();
            _requestLogger?.Stop();
            _webhookNotifier?.Stop();
        }
    }
}
