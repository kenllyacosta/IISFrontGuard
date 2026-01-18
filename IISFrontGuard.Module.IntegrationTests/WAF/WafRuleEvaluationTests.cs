using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.WAF
{
    [Collection("IIS Integration Tests")]
    public class WafRuleEvaluationTests : IDisposable
    {
        private readonly TestWebhookServer _webhookServer;
        private readonly IRequestLogger _requestLogger;
        private readonly IWebhookNotifier _webhookNotifier;
        private readonly IGeoIPService _geoIPService;
        private readonly IWafRuleRepository _wafRuleRepository;
        private readonly ICacheProvider _tokenCache;
        private readonly IConfigurationProvider _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public WafRuleEvaluationTests()
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
        public void EvaluateConditions_WithOrOperator_ShouldRequireOneCondition()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/api/test", "POST");
            var conditions = new List<WafCondition>
            {
                new WafCondition { FieldId = 7, OperatorId = 1, Valor = "get", LogicOperator = 2 },
                new WafCondition { FieldId = 13, OperatorId = 3, Valor = "/api/", LogicOperator = 2 }
            };

            // Act
            var result = module.EvaluateConditions(conditions, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void DetermineSeverityFromRule_WithHighPriority_ShouldReturnCritical()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var rule = new WafRule { Prioridad = 5 };

            // Act
            var result = module.DetermineSeverityFromRule(rule);

            // Assert
            Assert.Equal(SecurityEventSeverity.Critical, result);
        }

        [Fact]
        public void DetermineSeverityFromRule_WithMediumPriority_ShouldReturnHigh()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var rule = new WafRule { Prioridad = 25 };

            // Act
            var result = module.DetermineSeverityFromRule(rule);

            // Assert
            Assert.Equal(SecurityEventSeverity.High, result);
        }

        [Fact]
        public void DetermineSeverityFromRule_WithLowPriority_ShouldReturnMedium()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var rule = new WafRule { Prioridad = 75 };

            // Act
            var result = module.DetermineSeverityFromRule(rule);

            // Assert
            Assert.Equal(SecurityEventSeverity.Medium, result);
        }

        [Fact]
        public void HandleRuleAction_WithSkipAction_ShouldLogAndProceed()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var response = new System.Web.HttpResponse(new StringWriter());
            var rule = new WafRule { Id = 1, Nombre = "Skip Rule", ActionId = 1, AppId = Guid.NewGuid() };
            var rayId = Guid.NewGuid().ToString();

            // Act
            module.HandleRuleAction(rule, request, response, rayId, "US");

            // Assert - Should not throw or block
            Assert.NotEqual(403, response.StatusCode);
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
