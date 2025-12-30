using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using System;
using System.IO;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.WAF
{
    [Collection("IIS Integration Tests")]
    public class WafConditionEvaluationTests : IDisposable
    {
        private readonly TestWebhookServer _webhookServer;
        private readonly IRequestLogger _requestLogger;
        private readonly IWebhookNotifier _webhookNotifier;
        private readonly IGeoIPService _geoIPService;
        private readonly IWafRuleRepository _wafRuleRepository;
        private readonly ICacheProvider _tokenCache;
        private readonly IConfigurationProvider _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public WafConditionEvaluationTests()
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
        public void EvaluateCondition_WithEqualsOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 7, // method
                OperatorId = 1, // equals
                Valor = "get"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithDoesNotEqualOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "POST");
            var condition = new WafCondition
            {
                FieldId = 7, // method
                OperatorId = 2, // does not equal
                Valor = "get"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EvaluateCondition_WithContainsOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test/path", "GET");
            var condition = new WafCondition
            {
                FieldId = 13, // url
                OperatorId = 3, // contains
                Valor = "/test/"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithDoesNotContainOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test/path", "GET");
            var condition = new WafCondition
            {
                FieldId = 13, // url
                OperatorId = 4, // does not contain
                Valor = "/admin/"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithRegexOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/api/v1/users", "GET");
            var condition = new WafCondition
            {
                FieldId = 13, // url
                OperatorId = 5, // matches regex
                Valor = @"^/api/v\d+/"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithDoesNotMatchRegexOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/normal/path", "GET");
            var condition = new WafCondition
            {
                FieldId = 13, // url
                OperatorId = 6, // does not match regex
                Valor = @"^/admin/"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithStartsWithOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/api/users", "GET");
            var condition = new WafCondition
            {
                FieldId = 13, // url
                OperatorId = 7, // starts with
                Valor = "/api/"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithDoesNotStartWithOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/users", "GET");
            var condition = new WafCondition
            {
                FieldId = 13, // url
                OperatorId = 8, // does not start with
                Valor = "/api/"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithEndsWithOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/file.php", "GET");
            var condition = new WafCondition
            {
                FieldId = 13, // url
                OperatorId = 9, // ends with
                Valor = ".php"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithDoesNotEndWithOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/file.html", "GET");
            var condition = new WafCondition
            {
                FieldId = 13, // url
                OperatorId = 10, // does not end with
                Valor = ".php"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithIsInOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "POST");
            var condition = new WafCondition
            {
                FieldId = 7, // method
                OperatorId = 11, // is in
                Valor = "get,post,put"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithIsNotInOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 7, // method
                OperatorId = 12, // is not in
                Valor = "post,put,delete"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithIsInListOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "POST");
            var condition = new WafCondition
            {
                FieldId = 7, // method
                OperatorId = 13, // is in list
                Valor = "get, post, put"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithIsNotInListOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 7, // method
                OperatorId = 14, // is not in list
                Valor = "post, put, delete"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithIpInRangeOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", clientIp: "192.168.1.100");
            var condition = new WafCondition
            {
                FieldId = 3, // ip
                OperatorId = 15, // is ip in range
                Valor = "192.168.1.0/24"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithIpNotInRangeOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", clientIp: "10.0.0.1");
            var condition = new WafCondition
            {
                FieldId = 3, // ip
                OperatorId = 16, // is ip not in range
                Valor = "192.168.1.0/24"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithGreaterThanOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 19, // body length
                OperatorId = 17, // greater than
                Valor = "0"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert - Empty request body, so length is 0, not greater than 0
            Assert.False(result);
        }

        [Fact]
        public void EvaluateCondition_WithGreaterThanOperator_ShouldNotReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService,
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 19, // body length
                OperatorId = 17, // greater than
                Valor = "Test"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert - Empty request body, so length is 0, not greater than 0
            Assert.False(result);
        }

        [Fact]
        public void EvaluateCondition_WithLessThanOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 19, // body length
                OperatorId = 18, // less than
                Valor = "1000"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithLessThanOperator_ShouldNotReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService,
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 19, // body length
                OperatorId = 18, // less than
                Valor = "Test"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EvaluateCondition_WithGreaterThanOrEqualOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 19, // body length
                OperatorId = 19, // greater than or equal to
                Valor = "0"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithLessThanOrEqualOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 19, // body length
                OperatorId = 20, // less than or equal to
                Valor = "0"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithLessThanOrEqualOperator_ShouldNotReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService,
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 19, // body length
                OperatorId = 20, // less than or equal to
                Valor = "Test"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EvaluateCondition_WithNoValueOperator_ShouldNotReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService,
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 19, // body length
                OperatorId = 19, // less than or equal to
                Valor = "Test"
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EvaluateCondition_WithIsPresentOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET", userAgent: "TestAgent");
            var condition = new WafCondition
            {
                FieldId = 9, // user-agent
                OperatorId = 21, // is present
                Valor = ""
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateCondition_WithIsNotPresentOperator_ShouldReturnCorrectResult()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var request = TestHelpers.CreateMockHttpRequest("http://localhost/test", "GET");
            var condition = new WafCondition
            {
                FieldId = 6, // referrer
                OperatorId = 22, // is not present
                Valor = ""
            };

            // Act
            var result = module.EvaluateCondition(condition, request);

            // Assert
            Assert.True(result);
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
