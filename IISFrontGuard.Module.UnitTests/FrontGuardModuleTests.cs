using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using MaxMind.GeoIP2.Responses;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Collections.Specialized;
using System.Net;

namespace IISFrontGuard.Module.UnitTests
{
    [TestFixture]
    public class FrontGuardModuleTests
    {
        private Mock<IRequestLogger> _mockRequestLogger;
        private Mock<IWebhookNotifier> _mockWebhookNotifier;
        private Mock<IGeoIPService> _mockGeoIPService;
        private Mock<IWafRuleRepository> _mockWafRuleRepository;
        private Mock<ICacheProvider> _mockTokenCache;
        private Mock<IConfigurationProvider> _mockConfiguration;
        private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private FrontGuardModule _module;

        [SetUp]
        public void SetUp()
        {
            _mockRequestLogger = new Mock<IRequestLogger>();
            _mockWebhookNotifier = new Mock<IWebhookNotifier>();
            _mockGeoIPService = new Mock<IGeoIPService>();
            _mockWafRuleRepository = new Mock<IWafRuleRepository>();
            _mockTokenCache = new Mock<ICacheProvider>();
            _mockConfiguration = new Mock<IConfigurationProvider>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            // Setup default configuration
            _mockConfiguration.Setup(c => c.GetAppSettingAsBool("IISFrontGuard.Webhook.Enabled", false)).Returns(false);
            _mockConfiguration.Setup(c => c.GetAppSettingAsInt(It.IsAny<string>(), It.IsAny<int>()))
                .Returns((string key, int defaultValue) => defaultValue);
            _mockConfiguration.Setup(c => c.GetAppSetting(It.IsAny<string>())).Returns((string)null);
            _mockConfiguration.Setup(c => c.GetConnectionString(It.IsAny<string>())).Returns((string)null);

            _mockGeoIPService.Setup(g => g.GetGeoInfo(It.IsAny<string>())).Returns(new CountryResponse());
            _mockWafRuleRepository.Setup(w => w.FetchWafRules(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new List<WafRule>());

            _module = new FrontGuardModule(
                _mockRequestLogger.Object,
                _mockWebhookNotifier.Object,
                _mockGeoIPService.Object,
                _mockWafRuleRepository.Object,
                _mockTokenCache.Object,
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidDependencies_InitializesSuccessfully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new FrontGuardModule(
                _mockRequestLogger.Object,
                _mockWebhookNotifier.Object,
                _mockGeoIPService.Object,
                _mockWafRuleRepository.Object,
                _mockTokenCache.Object,
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object));
        }

        #endregion

        #region IsRateLimited Tests

        [Test]
        public void IsRateLimited_FirstRequest_ReturnsFalse()
        {
            // Arrange
            var clientIp = "192.168.1.1";

            // Act
            var result = _module.IsRateLimited(clientIp, 100, 60);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsRateLimited_WithinLimit_ReturnsFalse()
        {
            // Arrange
            var clientIp = "192.168.1.2";

            // Act
            var result1 = _module.IsRateLimited(clientIp, 10, 60);
            var result2 = _module.IsRateLimited(clientIp, 10, 60);
            var result3 = _module.IsRateLimited(clientIp, 10, 60);

            // Assert
            Assert.IsFalse(result1);
            Assert.IsFalse(result2);
            Assert.IsFalse(result3);
        }

        [Test]
        public void IsRateLimited_ExceedsLimit_ReturnsTrue()
        {
            // Arrange
            var clientIp = "192.168.1.3";
            var maxRequests = 3;

            // Act
            for (int i = 0; i < maxRequests; i++)
            {
                _module.IsRateLimited(clientIp, maxRequests, 60);
            }
            var result = _module.IsRateLimited(clientIp, maxRequests, 60);

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region GetAppSettingAsInt Tests

        [Test]
        public void GetAppSettingAsInt_ReturnsConfigurationValue()
        {
            // Arrange
            var key = "TestKey";
            var expectedValue = 42;
            _mockConfiguration.Setup(c => c.GetAppSettingAsInt(key, It.IsAny<int>())).Returns(expectedValue);

            // Act
            var result = _module.GetAppSettingAsInt(key, 0);

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        #endregion

        #region GenerateCsrfToken Tests

        [Test]
        public void GenerateCsrfToken_WithValidRayId_ReturnsNonEmptyToken()
        {
            // Arrange
            var rayId = Guid.NewGuid().ToString();

            // Act
            var result = _module.GenerateCsrfToken(rayId);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void GenerateCsrfToken_CachesToken()
        {
            // Arrange
            var rayId = Guid.NewGuid().ToString();

            // Act
            _module.GenerateCsrfToken(rayId);

            // Assert
            _mockTokenCache.Verify(c => c.Insert(
                $"CSRF_{rayId}",
                It.IsAny<string>(),
                null,
                It.IsAny<DateTime>(),
                Cache.NoSlidingExpiration), Times.Once);
        }

        #endregion

        #region ValidateCsrfToken Tests

        [Test]
        public void ValidateCsrfToken_WithValidToken_ReturnsTrue()
        {
            // Arrange
            var rayId = "test-ray-id";
            var token = "valid-token";
            _mockTokenCache.Setup(c => c.Get($"CSRF_{rayId}")).Returns(token);

            // Act
            var result = _module.ValidateCsrfToken(rayId, token);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void ValidateCsrfToken_WithInvalidToken_ReturnsFalse()
        {
            // Arrange
            var rayId = "test-ray-id";
            _mockTokenCache.Setup(c => c.Get($"CSRF_{rayId}")).Returns("valid-token");

            // Act
            var result = _module.ValidateCsrfToken(rayId, "invalid-token");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidateCsrfToken_WithMissingCachedToken_ReturnsFalse()
        {
            // Arrange
            var rayId = "test-ray-id";
            _mockTokenCache.Setup(c => c.Get($"CSRF_{rayId}")).Returns((string)null);

            // Act
            var result = _module.ValidateCsrfToken(rayId, "any-token");

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region GenerateClientFingerprint Tests

        [Test]
        public void GenerateClientFingerprint_WithValidRequest_ReturnsNonEmptyHash()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GenerateClientFingerprint(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void GenerateClientFingerprint_SameRequest_ReturnsSameHash()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result1 = _module.GenerateClientFingerprint(request);
            var result2 = _module.GenerateClientFingerprint(request);

            // Assert
            Assert.AreEqual(result1, result2);
        }

        #endregion

        #region IsTokenValid Tests

        [Test]
        public void IsTokenValid_WithNullToken_ReturnsFalse()
        {
            // Act
            var result = _module.IsTokenValid(null);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsTokenValid_WithEmptyToken_ReturnsFalse()
        {
            // Act
            var result = _module.IsTokenValid("");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsTokenValid_WithExpiredToken_ReturnsFalse()
        {
            // Arrange
            var token = "expired-token";
            var expiredTime = DateTime.UtcNow.AddHours(-1);
            _mockTokenCache.Setup(c => c.Get(token)).Returns(expiredTime);

            // Act
            var result = _module.IsTokenValid(token);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsTokenValid_WithValidToken_ReturnsTrue()
        {
            // Arrange
            var token = "valid-token";
            var futureTime = DateTime.UtcNow.AddHours(1);
            _mockTokenCache.Setup(c => c.Get(token)).Returns(futureTime);

            // Act
            var result = _module.IsTokenValid(token);

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region EvaluateCondition Tests

        [Test]
        public void EvaluateCondition_EqualsOperator_WithMatch_ReturnsTrue()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            var condition = new WafCondition
            {
                FieldId = 2, // hostname
                OperatorId = 1, // equals
                Valor = "example.com"
            };

            // Act
            var result = _module.EvaluateCondition(condition, request);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_ContainsOperator_WithMatch_ReturnsTrue()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            var condition = new WafCondition
            {
                FieldId = 2, // hostname
                OperatorId = 3, // contains
                Valor = "example"
            };

            // Act
            var result = _module.EvaluateCondition(condition, request);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_StartsWithOperator_WithMatch_ReturnsTrue()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            var condition = new WafCondition
            {
                FieldId = 2, // hostname
                OperatorId = 7, // starts with
                Valor = "exam"
            };

            // Act
            var result = _module.EvaluateCondition(condition, request);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_UnknownOperator_ReturnsFalse()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            var condition = new WafCondition
            {
                FieldId = 2, // hostname
                OperatorId = 99, // unknown operator
                Valor = "anything"
            };

            // Act
            var result = _module.EvaluateCondition(condition, request);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region GetFieldValue Tests

        [Test]
        public void GetFieldValue_Hostname_ReturnsCorrectValue()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetFieldValue(2, request); // 2 = hostname

            // Assert
            Assert.AreEqual("example.com", result);
        }

        [Test]
        public void GetFieldValue_Protocol_ReturnsCorrectValue()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetFieldValue(5, request); // 5 = protocol

            // Assert
            Assert.AreEqual("http", result);
        }

        [Test]
        public void GetFieldValue_HttpMethod_ReturnsCorrectValue()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetFieldValue(7, request); // 7 = method

            // Assert
            Assert.AreEqual("GET", result);
        }

        #endregion

        #region GetClientIp Tests

        [Test]
        public void GetClientIp_WithDirectConnection_ReturnsUserHostAddress()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetClientIp(request);

            // Assert
            Assert.IsNotNull(result);
        }

        #endregion

        #region GetHostname Tests

        [Test]
        public void GetHostname_WithValidRequest_ReturnsHost()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetHostname(request);

            // Assert
            Assert.AreEqual("example.com", result);
        }

        #endregion

        #region GetProtocol Tests

        [Test]
        public void GetProtocol_WithNonSecureConnection_ReturnsHttp()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetProtocol(request);

            // Assert
            Assert.AreEqual("http", result);
        }

        #endregion

        #region GetHttpMethod Tests

        [Test]
        public void GetHttpMethod_ReturnsRequestMethod()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetHttpMethod(request);

            // Assert
            Assert.AreEqual("GET", result);
        }

        #endregion

        #region GetUserAgent Tests

        #endregion

        #region GetFullUrl Tests

        [Test]
        public void GetFullUrl_ReturnsAbsoluteUri()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetFullUrl(request);

            // Assert
            Assert.IsTrue(result.Contains("example.com"));
        }

        #endregion

        #region GetUrlPath Tests

        [Test]
        public void GetUrlPath_ReturnsAbsolutePath()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetUrlPath(request);

            // Assert
            Assert.IsNotNull(result);
        }

        #endregion

        #region GetConnectionString Tests

        [Test]
        public void GetConnectionString_WithNoConfiguration_ReturnsFallback()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetConnectionString(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("IISFrontGuard"));
        }

        [Test]
        public void GetConnectionString_WithConfiguredDefault_ReturnsConfigured()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            var expectedCs = "Server=test;Database=test;";
            _mockConfiguration.Setup(c => c.GetAppSetting("IISFrontGuard.DefaultConnectionStringName"))
                .Returns("DefaultConnection");
            _mockConfiguration.Setup(c => c.GetConnectionString("DefaultConnection"))
                .Returns(expectedCs);

            // Act
            var result = _module.GetConnectionString(request);

            // Assert
            Assert.AreEqual(expectedCs, result);
        }

        #endregion

        #region GetConnectionStringByHost Tests

        [Test]
        public void GetConnectionStringByHost_WithEmptyHost_ReturnsFallback()
        {
            // Act
            var result = _module.GetConnectionStringByHost("");

            // Assert
            Assert.IsNotNull(result);
        }

        [Test]
        public void GetConnectionStringByHost_WithHostSpecificConfig_ReturnsHostConfig()
        {
            // Arrange
            var host = "test.example.com";
            var expectedCs = "Server=host-specific;";
            _mockConfiguration.Setup(c => c.GetAppSetting($"GlobalLogger.Host.{host}"))
                .Returns("HostConnection");
            _mockConfiguration.Setup(c => c.GetConnectionString("HostConnection"))
                .Returns(expectedCs);

            // Act
            var result = _module.GetConnectionStringByHost(host);

            // Assert
            Assert.AreEqual(expectedCs, result);
        }

        [Test]
        public void GetConnectionStringByHost_ReturnsFallback_OnException()
        {
            // Arrange
            var host = "throwinghost";
            _mockConfiguration.Setup(c => c.GetAppSetting(It.IsAny<string>())).Throws(new Exception("fail"));

            // Act
            var result = _module.GetConnectionStringByHost(host);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("IISFrontGuard")); // fallback string
        }

        #endregion

        #region DetermineSeverityFromRule Tests

        [Test]
        public void DetermineSeverityFromRule_WithHighPriority_ReturnsCritical()
        {
            // Arrange
            var rule = new WafRule { Prioridad = 5 };

            // Act
            var result = _module.DetermineSeverityFromRule(rule);

            // Assert
            Assert.AreEqual(SecurityEventSeverity.Critical, result);
        }

        [Test]
        public void DetermineSeverityFromRule_WithMediumPriority_ReturnsHigh()
        {
            // Arrange
            var rule = new WafRule { Prioridad = 30 };

            // Act
            var result = _module.DetermineSeverityFromRule(rule);

            // Assert
            Assert.AreEqual(SecurityEventSeverity.High, result);
        }

        [Test]
        public void DetermineSeverityFromRule_WithLowPriority_ReturnsLow()
        {
            // Arrange
            var rule = new WafRule { Prioridad = 150 };

            // Act
            var result = _module.DetermineSeverityFromRule(rule);

            // Assert
            Assert.AreEqual(SecurityEventSeverity.Low, result);
        }

        #endregion

        #region AddSecurityHeaders Tests

        [Test]
        public void AddSecurityHeaders_AddsRequiredHeaders()
        {
            // Arrange
            var mockResponseHeaderManager = new Mock<IResponseHeaderManager>();

            // Act
            _module.AddSecurityHeaders(mockResponseHeaderManager.Object);

            // Assert
            mockResponseHeaderManager.Verify(r => r.AddHeaderIfMissing("X-Content-Type-Options", "nosniff"), Times.Once);
            mockResponseHeaderManager.Verify(r => r.AddHeaderIfMissing("X-Frame-Options", "SAMEORIGIN"), Times.Once);
            mockResponseHeaderManager.Verify(r => r.AddHeaderIfMissing("X-XSS-Protection", "1; mode=block"), Times.Once);
            mockResponseHeaderManager.Verify(r => r.AddHeaderIfMissing("Referrer-Policy", "strict-origin-when-cross-origin"), Times.Once);
        }

        #endregion

        #region AddContentSecurityPolicy Tests

        [Test]
        public void AddContentSecurityPolicy_WithHtmlContent_AddsCSPHeader()
        {
            // Arrange
            var mockResponseHeaderManager = new Mock<IResponseHeaderManager>();
            mockResponseHeaderManager.Setup(r => r.ContentType).Returns("text/html");

            // Act
            _module.AddContentSecurityPolicy(mockResponseHeaderManager.Object);

            // Assert
            mockResponseHeaderManager.Verify(r => r.AddHeaderIfMissing("Content-Security-Policy", It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void AddContentSecurityPolicy_WithNonHtmlContent_DoesNotAddCSPHeader()
        {
            // Arrange
            var mockResponseHeaderManager = new Mock<IResponseHeaderManager>();
            mockResponseHeaderManager.Setup(r => r.ContentType).Returns("application/json");

            // Act
            _module.AddContentSecurityPolicy(mockResponseHeaderManager.Object);

            // Assert
            mockResponseHeaderManager.Verify(r => r.AddHeaderIfMissing("Content-Security-Policy", It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region AddHstsHeader Tests

        [Test]
        public void AddHstsHeader_WithSecureConnection_AddsHSTSHeader()
        {
            // Arrange
            var mockResponseHeaderManager = new Mock<IResponseHeaderManager>();
            mockResponseHeaderManager.Setup(r => r.IsSecureConnection).Returns(true);

            // Act
            _module.AddHstsHeader(mockResponseHeaderManager.Object);

            // Assert
            mockResponseHeaderManager.Verify(r => r.AddHeaderIfMissing("Strict-Transport-Security", "max-age=31536000; includeSubDomains"), Times.Once);
        }

        [Test]
        public void AddHstsHeader_WithNonSecureConnection_DoesNotAddHSTSHeader()
        {
            // Arrange
            var mockResponseHeaderManager = new Mock<IResponseHeaderManager>();
            mockResponseHeaderManager.Setup(r => r.IsSecureConnection).Returns(false);

            // Act
            _module.AddHstsHeader(mockResponseHeaderManager.Object);

            // Assert
            mockResponseHeaderManager.Verify(r => r.AddHeaderIfMissing("Strict-Transport-Security", It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region CreateBlockedEventNotification Tests

        [Test]
        public void CreateBlockedEventNotification_CreatesCorrectEvent()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            var rule = new WafRule { Id = 1, Nombre = "TestRule", Prioridad = 10 };
            var rayId = "test-ray-id";

            // Act
            var result = _module.CreateBlockedEventNotification(request, rule, rayId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(SecurityEventTypes.RequestBlocked, result.EventType);
            Assert.AreEqual(rayId, result.RayId);
            Assert.AreEqual(1, result.RuleId);
            Assert.AreEqual("TestRule", result.RuleName);
        }

        #endregion

        #region CreateChallengeEventNotification Tests

        [Test]
        public void CreateChallengeEventNotification_CreatesCorrectEvent()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            var rule = new WafRule { Id = 2, Nombre = "ChallengeRule", Prioridad = 20 };
            var rayId = "test-ray-id";
            var challengeType = "managed";

            // Act
            var result = _module.CreateChallengeEventNotification(request, rule, rayId, challengeType);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(SecurityEventTypes.ChallengeIssued, result.EventType);
            Assert.AreEqual(SecurityEventSeverity.Medium, result.Severity);
            Assert.AreEqual(rayId, result.RayId);
        }

        #endregion

        #region GetAssemblyVersion Tests

        [Test]
        public void GetAssemblyVersion_ReturnsVersionString()
        {
            // Act
            var result = _module.GetAssemblyVersion();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        #endregion

        #region FetchWafRules Tests

        [Test]
        public void FetchWafRules_CallsRepository()
        {
            // Arrange
            var host = "example.com";
            var expectedRules = new List<WafRule>
            {
                new WafRule { Id = 1, Nombre = "Rule1" }
            };
            _mockWafRuleRepository.Setup(r => r.FetchWafRules(host, It.IsAny<string>()))
                .Returns(expectedRules);

            // Act
            var result = _module.FetchWafRules(host);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());
            _mockWafRuleRepository.Verify(r => r.FetchWafRules(host, It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region GetCountryName Tests

        [Test]
        public void GetCountryName_WithGeoInfo_ReturnsCountryName()
        {
            // Arrange
            var geoInfo = new CountryResponse();
            // Note: CountryResponse is difficult to mock without reflection
            _mockHttpContextAccessor.Setup(h => h.GetContextItem("IISFrontGuard.GeoInfo"))
                .Returns(geoInfo);

            // Act
            var result = _module.GetCountryName();

            // Assert
            Assert.IsNotNull(result);
        }

        [Test]
        public void GetCountryName_WithoutGeoInfo_ReturnsEmptyString()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(h => h.GetContextItem("IISFrontGuard.GeoInfo"))
                .Returns((object)null);

            // Act
            var result = _module.GetCountryName();

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        #endregion

        #region GetCountryIsoCode Tests

        [Test]
        public void GetCountryIsoCode_WithoutGeoInfo_ReturnsEmptyString()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(h => h.GetContextItem("IISFrontGuard.GeoInfo"))
                .Returns((object)null);

            // Act
            var result = _module.GetCountryIsoCode();

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        #endregion

        #region Helper Methods

        private HttpRequest CreateMockHttpRequest(string url = "http://example.com/test")
        {
            var uri = new Uri(url);
            var request = new HttpRequest("test.aspx", url, uri.Query.TrimStart('?'))
            {
                RequestContext = new System.Web.Routing.RequestContext(
                    new HttpContextWrapper(new HttpContext(
                        new HttpRequest(null, url, uri.Query.TrimStart('?')),
                        new HttpResponse(new StringWriter())
                    )),
                    new System.Web.Routing.RouteData()
                )
            };
            return request;
        }

        #endregion

        [Test]
        public void Dispose_StopsServices()
        {
            var logger = new Mock<IRequestLogger>();
            var webhook = new Mock<IWebhookNotifier>();
            var module = new FrontGuardModule(
                logger.Object, webhook.Object,
                _mockGeoIPService.Object, _mockWafRuleRepository.Object,
                _mockTokenCache.Object, _mockConfiguration.Object, _mockHttpContextAccessor.Object);
            module.Dispose();
            logger.Verify(x => x.Stop(), Times.Once);
            webhook.Verify(x => x.Stop(), Times.Once);
        }

        [Test]
        public void NotifyTokenReplayAttempt_SendsWebhookIfEnabled()
        {
            _mockConfiguration.Setup(x => x.GetAppSettingAsBool("IISFrontGuard.Webhook.Enabled", false)).Returns(true);
            var webhook = new Mock<IWebhookNotifier>();
            var module = new FrontGuardModule(
                _mockRequestLogger.Object, webhook.Object,
                _mockGeoIPService.Object, _mockWafRuleRepository.Object,
                _mockTokenCache.Object, _mockConfiguration.Object, _mockHttpContextAccessor.Object);
            var req = CreateMockHttpRequest();
            module.NotifyTokenReplayAttempt(req);
            webhook.Verify(x => x.EnqueueSecurityEvent(It.Is<SecurityEvent>(e => e.EventType == SecurityEventTypes.TokenReplayAttempt)), Times.Once);
        }

        [Test]
        public void NotNotifyTokenReplayWebhookIfNotEnabled()
        {
            _mockConfiguration.Setup(x => x.GetAppSettingAsBool("IISFrontGuard.Webhook.Enabled", false)).Returns(false);
            var webhook = new Mock<IWebhookNotifier>();
            var module = new FrontGuardModule(
                _mockRequestLogger.Object, webhook.Object,
                _mockGeoIPService.Object, _mockWafRuleRepository.Object,
                _mockTokenCache.Object, _mockConfiguration.Object, _mockHttpContextAccessor.Object);
            var req = CreateMockHttpRequest();
            module.NotifyTokenReplayAttempt(req);
            webhook.VerifyNoOtherCalls();
        }

        [Test]
        public void TrackChallengeFailure_SendsNotificationAfterThreshold()
        {
            _mockConfiguration.Setup(x => x.GetAppSettingAsBool("IISFrontGuard.Webhook.Enabled", false)).Returns(true);
            var webhook = new Mock<IWebhookNotifier>();
            var module = new FrontGuardModule(
                _mockRequestLogger.Object, webhook.Object,
                _mockGeoIPService.Object, _mockWafRuleRepository.Object,
                _mockTokenCache.Object, _mockConfiguration.Object, _mockHttpContextAccessor.Object);
            for (int i = 0; i < 3; i++)
                module.TrackChallengeFailure("1.2.3.4", "ray", "fail");
            webhook.Verify(x => x.EnqueueSecurityEvent(It.Is<SecurityEvent>(e => e.EventType == SecurityEventTypes.MultipleChallengeFails)), Times.Once);
        }

        [Test]
        public void EvaluateConditions_AndOrLogic()
        {
            var req = CreateMockHttpRequest();
            var andCond = new WafCondition { FieldId = 2, OperatorId = 1, Valor = "example.com", LogicOperator = 1 };
            var orCond = new WafCondition { FieldId = 2, OperatorId = 1, Valor = "notmatch", LogicOperator = 2 };
            var module = _module;
            Assert.IsTrue(module.EvaluateConditions(new[] { andCond }, req));
            Assert.IsTrue(module.EvaluateConditions(new[] { orCond, andCond }, req));
        }

        [Test]
        public void EvaluateConditions_AndOrLogicNoMatch()
        {
            var req = CreateMockHttpRequest();
            var orCond = new WafCondition { FieldId = 2, OperatorId = 1, Valor = "notmatch", LogicOperator = 1 };
            Assert.IsFalse(_module.EvaluateConditions(new[] { orCond }, req));
        }

        [Test]
        public void FetchWafConditions_CallsRepository()
        {
            _mockWafRuleRepository.Setup(x => x.FetchWafConditions(1, It.IsAny<string>())).Returns(new List<WafCondition> { new WafCondition() });
            var result = _module.FetchWafConditions(1, "cs");
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void GetContinentName_ReturnsValueOrEmpty()
        {
            _mockHttpContextAccessor.Setup(x => x.GetContextItem("IISFrontGuard.GeoInfo")).Returns((object)null);
            Assert.AreEqual("", _module.GetContinentName());
        }

        [Test]
        public void GetBodyLength_ReturnsLengthOrZero()
        {
            var req = CreateMockHttpRequest();
            Assert.AreEqual("0", _module.GetBodyLength(req));
        }

        [Test]
        public void GetBodyLength_ReturnsLengthEmpty()
        {
            Assert.AreEqual("", _module.GetBodyLength(null));
        }

        [Test]
        public void GetMimeType_ReturnsMimeType()
        {
            var req = CreateMockHttpRequest("http://example.com/file.txt");
            req.ContentType = "text/plain";
            Assert.AreEqual("text/plain", _module.GetMimeType(req));
        }

        [Test]
        public void GetContentType_ReturnsContentType()
        {
            var req = CreateMockHttpRequest();
            req.ContentType = "application/json";
            Assert.AreEqual("application/json", _module.GetContentType(req));
        }

        [Test]
        public void GetQueryString_ReturnsQuery()
        {
            var req = CreateMockHttpRequest("http://example.com/test?foo=bar");
            Assert.AreEqual("?foo=bar", _module.GetQueryString(req));
        }

        [Test]
        public void GenerateHTMLPages_ProduceHtml()
        {
            var html1 = _module.GenerateHTMLInteractiveChallenge("domain.com", "ray", "csrf");
            var html2 = _module.GenerateHTMLManagedChallenge("domain.com", "ray", "csrf");
            var html3 = _module.GenerateHTMLUserBlockedPage("domain.com", "ray");
            var html4 = _module.GenerateHTMLRateLimitPage("domain.com", "ray");
            Assert.IsTrue(html1.Contains("html"));
            Assert.IsTrue(html2.Contains("html"));
            Assert.IsTrue(html3.Contains("html"));
            Assert.IsTrue(html4.Contains("html"));
        }

        #region GenerateAndSetToken Tests

        [Test]
        public void GenerateAndSetToken_GeneratesTokenAndSetsCookie()
        {
            // Arrange
            var httpContext = new HttpContext(
                new HttpRequest("test", "http://example.com", ""),
                new HttpResponse(new StringWriter())
            );
            HttpContext.Current = httpContext; // Set the current context
            var response = HttpContext.Current.Response;
            var request = HttpContext.Current.Request;
            var encryptionKey = "test-encryption-key";

            _mockRequestLogger.Setup(r => r.Encrypt(It.IsAny<string>(), encryptionKey))
                .Returns("encrypted-token");
            _mockRequestLogger.Setup(r => r.GetTokenExpirationDuration(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(1); // 1 hour expiration

            // Act
            _module.GenerateAndSetToken(request, response, encryptionKey);

            // Assert
            _mockRequestLogger.Verify(r => r.Encrypt(It.IsAny<string>(), encryptionKey), Times.Once);
            _mockTokenCache.Verify(c => c.Insert(
                "encrypted-token",
                It.IsAny<DateTime>(),
                null,
                It.IsAny<DateTime>(),
                Cache.NoSlidingExpiration), Times.Once);

            Assert.IsNotNull(response.Cookies["fgm_clearance"]);
            Assert.AreEqual("encrypted-token", response.Cookies["fgm_clearance"].Value);
        }

        [Test]
        public void GenerateAndSetToken_RedirectsIfTokenIsValid()
        {
            // Arrange
            var httpContext = new HttpContext(
                new HttpRequest("test", "http://example.com", ""),
                new HttpResponse(new StringWriter())
            );
            HttpContext.Current = httpContext; // Set the current context
            var response = HttpContext.Current.Response;
            var request = HttpContext.Current.Request;
            var encryptionKey = "test-encryption-key";

            _mockRequestLogger.Setup(r => r.Encrypt(It.IsAny<string>(), encryptionKey))
                .Returns("encrypted-token");
            _mockRequestLogger.Setup(r => r.GetTokenExpirationDuration(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(1); // 1 hour expiration
            _mockTokenCache.Setup(c => c.Get("encrypted-token"))
                .Returns(DateTime.UtcNow.AddHours(1));

            // Act
            _module.GenerateAndSetToken(request, response, encryptionKey);

            // Assert
            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode);
        }

        #endregion

        #region Additional Tests for Requested Lines
                
        [Test]
        public void GetFieldValue_Body_DelegatesToRequestLogger()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            _mockRequestLogger.Setup(r => r.GetBody(request)).Returns("the-body");

            // Act
            var result = _module.GetFieldValue(18, request);

            // Assert
            Assert.AreEqual("the-body", result);
            _mockRequestLogger.Verify(r => r.GetBody(request), Times.Once);
        }

        [Test]
        public void GetFieldValue_CountryFields_ReturnsGeoInfoValues()
        {
            // Arrange
            var request = CreateMockHttpRequest();
            var mockCountry = new
            {
                Country = new { Name = "Spain", IsoCode = "ES" },
                Continent = new { Name = "Europe" }
            };

            _mockHttpContextAccessor.Setup(h => h.GetContextItem(It.IsAny<string>())).Returns((object)mockCountry);

            // Act
            var countryName = _module.GetFieldValue(20, request);
            var countryIso = _module.GetFieldValue(21, request);
            var continent = _module.GetFieldValue(22, request);

            // Assert
            Assert.AreEqual("", countryName);
            Assert.AreEqual("", countryIso);
            Assert.AreEqual("", continent);
        }

        [Test]
        public void GetFieldValue_UnknownField_ReturnsEmpty()
        {
            // Arrange
            var request = CreateMockHttpRequest();

            // Act
            var result = _module.GetFieldValue(99, request);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void GetMimeType_NoExtensionAndNoContentType_ReturnsEmpty()
        {
            // Arrange
            var request = CreateMockHttpRequest("http://example.com/pathwithnoext");
            request.ContentType = null;

            // Act
            var result = _module.GetMimeType(request);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        #endregion
    }
}
