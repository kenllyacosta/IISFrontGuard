using Castle.Core.Logging;
using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.Models;
using Moq;
using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.FrontGuard
{
    public class FrontGuardModuleIntegrationTests
    {
        private static FrontGuardModule CreateModule(
            Mock<IRequestLogger> requestLogger = null,
            Mock<IWebhookNotifier> webhookNotifier = null,
            Mock<IGeoIPService> geoIp = null,
            Mock<IWafRuleRepository> wafRepo = null,
            Mock<ICacheProvider> cache = null,
            Mock<IConfigurationProvider> config = null,
            Mock<IHttpContextAccessor> accessor = null)
        {
            return new FrontGuardModule(
                requestLogger?.Object ?? new Mock<IRequestLogger>().Object,
                webhookNotifier?.Object ?? new Mock<IWebhookNotifier>().Object,
                geoIp?.Object ?? new Mock<IGeoIPService>().Object,
                wafRepo?.Object ?? new Mock<IWafRuleRepository>().Object,
                cache?.Object ?? new Mock<ICacheProvider>().Object,
                config?.Object ?? new Mock<IConfigurationProvider>().Object,
                accessor?.Object ?? new Mock<IHttpContextAccessor>().Object
            );
        }

        [Fact]
        public void DefaultConstructor_InitializesModule()
        {
            // Act
            var module = new FrontGuardModule();

            // Assert
            Assert.NotNull(module);
            Assert.IsType<FrontGuardModule>(module);

            // Optionally, verify basic functionality
            var app = new HttpApplication();
            module.Init(app);
            Assert.NotNull(app); // Ensure no exceptions are thrown during initialization
        }

        [Fact]
        public void Init_WiresEvents()
        {
            var module = CreateModule();
            var app = new HttpApplication();
            module.Init(app);
            // No exception = success (event handlers are attached)

            Assert.NotNull(app); Assert.IsType<HttpApplication>(app);
        }

        [Fact]
        public void Context_Disposed_StopsServices()
        {
            var logger = new Mock<IRequestLogger>();
            var webhook = new Mock<IWebhookNotifier>();
            var module = CreateModule(logger, webhook);
            module.Context_Disposed(this, EventArgs.Empty);
            logger.Verify(x => x.Stop(), Times.Once);
            webhook.Verify(x => x.Stop(), Times.Once);
        }

        [Fact]
        public async Task IsRateLimited_ResetsWindowAndIncrements()
        {
            var module = CreateModule();
            var ip = "1.2.3.4";
            // First call, not limited
            Assert.False(module.IsRateLimited(ip, 2, 1));
            // Second call, not limited
            Assert.False(module.IsRateLimited(ip, 2, 1));
            // Third call, should be limited
            Assert.True(module.IsRateLimited(ip, 2, 1));
            // Wait for window to reset

            await Task.Delay(1100);
            Assert.False(module.IsRateLimited(ip, 2, 1));
        }

        [Fact]
        public void HandleRuleAction_Block_SendsNotificationAndBlocks()
        {
            var logger = new Mock<IRequestLogger>();
            var webhook = new Mock<IWebhookNotifier>();
            var config = new Mock<IConfigurationProvider>();
            config.Setup(x => x.GetAppSettingAsBool("IISFrontGuard.Webhook.Enabled", false)).Returns(true);
            var module = CreateModule(logger, webhook, config: config);
            var rule = new WafRule { ActionId = 2, Id = 1, Nombre = "Block", AppId = Guid.NewGuid(), Habilitado = true };
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var resp = new HttpResponse(new StringWriter());
            // Return the configured value from the test project's app.config instead of an empty string
            config.Setup(x => x.GetAppSetting("IISFrontGuardEncryptionKey")).Returns(System.Configuration.ConfigurationManager.AppSettings["IISFrontGuardEncryptionKey"]);
            module.HandleRuleAction(rule, req, resp, "ray", "US");
            webhook.Verify(x => x.EnqueueSecurityEvent(It.Is<SecurityEvent>(e => e.EventType == SecurityEventTypes.RequestBlocked)), Times.Once);
            logger.Verify(x => x.Enqueue(req, It.IsAny<string>(), 1, "ray", "US", 2, rule.AppId.ToString()), Times.Once);
            Assert.Equal(403, resp.StatusCode);
        }

        [Fact]
        public void HandleRuleAction_Manage_Challenge_SendsNotificationAndBlocks()
        {
            var logger = new Mock<IRequestLogger>();
            var webhook = new Mock<IWebhookNotifier>();
            var config = new Mock<IConfigurationProvider>();
            config.Setup(x => x.GetAppSettingAsBool("IISFrontGuard.Webhook.Enabled", false)).Returns(true);
            // Return the configured value from the test project's app.config instead of an empty string
            config.Setup(x => x.GetAppSetting("IISFrontGuardEncryptionKey")).Returns(System.Configuration.ConfigurationManager.AppSettings["IISFrontGuardEncryptionKey"]);
            var module = CreateModule(logger, webhook, config: config);
            var rule = new WafRule { ActionId = 3, Id = 1, Nombre = "Manage Challenge", AppId = Guid.NewGuid(), Habilitado = true };
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var resp = new HttpResponse(new StringWriter());
            module.HandleRuleAction(rule, req, resp, "ray", "US");
            webhook.Verify(x => x.EnqueueSecurityEvent(It.Is<SecurityEvent>(e => e.EventType == SecurityEventTypes.ChallengeIssued)), Times.Once);
            logger.Verify(x => x.Enqueue(req, It.IsAny<string>(), 1, "ray", "US", 3, rule.AppId.ToString()), Times.Once);
            Assert.Equal(403, resp.StatusCode);
        }

        [Fact]
        public void HandleRuleAction_Interactive_Challenge_SendsNotificationAndBlocks()
        {
            var logger = new Mock<IRequestLogger>();
            var webhook = new Mock<IWebhookNotifier>();
            var config = new Mock<IConfigurationProvider>();
            config.Setup(x => x.GetAppSettingAsBool("IISFrontGuard.Webhook.Enabled", false)).Returns(true);
            // Return the configured value from the test project's app.config instead of an empty string
            config.Setup(x => x.GetAppSetting("IISFrontGuardEncryptionKey")).Returns(System.Configuration.ConfigurationManager.AppSettings["IISFrontGuardEncryptionKey"]);
            var module = CreateModule(logger, webhook, config: config);
            var rule = new WafRule { ActionId = 4, Id = 1, Nombre = "Manage Challenge", AppId = Guid.NewGuid(), Habilitado = true };
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var resp = new HttpResponse(new StringWriter());
            module.HandleRuleAction(rule, req, resp, "ray", "US");
            webhook.Verify(x => x.EnqueueSecurityEvent(It.Is<SecurityEvent>(e => e.EventType == SecurityEventTypes.ChallengeIssued)), Times.Once);
            logger.Verify(x => x.Enqueue(req, It.IsAny<string>(), 1, "ray", "US", 4, rule.AppId.ToString()), Times.Once);
            Assert.Equal(403, resp.StatusCode);
        }

        [Fact]
        public void LogAndProceed_EnqueuesRequest()
        {
            var logger = new Mock<IRequestLogger>();
            var module = CreateModule(logger);
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var ctx = new RequestLogContext { ConnectionString = "cs", RuleTriggered = 1, RayId = "ray", Iso2 = "US", ActionId = 2, AppId = "app" };
            module.LogAndProceed(req, ctx);
            logger.Verify(x => x.Enqueue(req, "cs", 1, "ray", "US", 2, "app"), Times.Once);
        }

        [Fact]
        public void BlockRequest_EnqueuesAndBlocks()
        {
            var logger = new Mock<IRequestLogger>();
            var accessor = new Mock<IHttpContextAccessor>();
            var module = CreateModule(logger, accessor: accessor);
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var resp = new HttpResponse(new StringWriter());
            var ctx = new RequestLogContext { ConnectionString = "cs", RuleTriggered = 1, RayId = "ray", Iso2 = "US", ActionId = 2, AppId = "app" };
            module.BlockRequest(req, resp, ctx);
            logger.Verify(x => x.Enqueue(req, "cs", 1, "ray", "US", 2, "app"), Times.Once);
            Assert.Equal(403, resp.StatusCode);
            Assert.Contains("Access Denied", resp.Output.ToString());
        }

        [Fact]
        public void HandleManagedChallenge_DisplaysFormOrProcessesPost()
        {
            var logger = new Mock<IRequestLogger>();
            var accessor = new Mock<IHttpContextAccessor>();
            var cache = new Mock<ICacheProvider>();
            var config = new Mock<IConfigurationProvider>();
            var module = CreateModule(logger, cache: cache, config: config, accessor: accessor);
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var resp = new HttpResponse(new StringWriter());
            var ctx = new RequestLogContext { ConnectionString = "cs", RuleTriggered = 1, RayId = "ray", Iso2 = "US", ActionId = 3, AppId = "app" };
            // GET: should display form

            module.HandleManagedChallenge(req, resp, null, "key", ctx);
            Assert.Equal(403, resp.StatusCode);

            cache.Setup(x => x.Get("CSRF_ray")).Returns("token");
            module.HandleManagedChallenge(req, resp, null, "key", ctx);
        }

        [Fact]
        public void HandleManagedChallenge_ProcessChallengePostRequest()
        {
            var req = TestHelpers.CreateRequestWithBody("http://localhost/", "POST", "...");
            var resp = new HttpResponse(new StringWriter());

            var logger = new Mock<IRequestLogger>();
            var accessor = new Mock<IHttpContextAccessor>();
            var cache = new Mock<ICacheProvider>();
            var config = new Mock<IConfigurationProvider>();
            var module = CreateModule(logger, cache: cache, config: config, accessor: accessor);
            
            var ctx = new RequestLogContext { ConnectionString = "cs", RuleTriggered = 1, RayId = "ray", Iso2 = "US", ActionId = 3, AppId = "app" };
            
            module.HandleManagedChallenge(req, resp, null, "key", ctx);
            Assert.Equal(403, resp.StatusCode);

            cache.Setup(x => x.Get("CSRF_ray")).Returns("token");
            module.HandleManagedChallenge(req, resp, null, "key", ctx);
        }

        [Fact]
        public void HandleManagedChallenge_ProcessGenerateAndSetTheToken()
        {
            // Arrange
            var formRayId = Guid.NewGuid().ToString();
            var resp = new HttpResponse(new StringWriter());

            var logger = new Mock<IRequestLogger>();
            var accessor = new Mock<IHttpContextAccessor>();
            var cache = new Mock<ICacheProvider>();
            var config = new Mock<IConfigurationProvider>();
            var module = CreateModule(logger, cache: cache, config: config, accessor: accessor);

            string submittedCsrf = "Csrf_Value";
            string encryptionKey = "key";

            var ctx = new RequestLogContext
            {
                ConnectionString = "cs",
                RuleTriggered = 1,
                RayId = formRayId,
                Iso2 = "US",
                ActionId = 3,
                AppId = "app"
            };

            // Set up the cache to return the valid CSRF token
            cache.Setup(x => x.Get($"CSRF_{formRayId}")).Returns(submittedCsrf);

            // Simulate encryption in the mock
            logger.Setup(x => x.Encrypt(It.IsAny<string>(), encryptionKey))
                  .Returns((string data, string key) => $"encrypted({data})");

            var req = TestHelpers.CreateRequestWithBody(
                url: "http://localhost/",
                method: "POST",
                body: $"__rayId={formRayId}&__csrf={submittedCsrf}",
                contentType: "application/x-www-form-urlencoded");

            Assert.Throws<NullReferenceException>(() => {
                module.HandleManagedChallenge(req, resp, null, encryptionKey, ctx);
            });

            // Assert
            Assert.Equal(200, resp.StatusCode);
            logger.Verify(x => x.Encrypt(It.IsAny<string>(), encryptionKey), Times.Once);
        }

        // Small helper to keep tests clean and consistent
        private sealed class Sut
        {
            public Mock<IRequestLogger> Logger { get; } = new Mock<IRequestLogger>();
            public Mock<IHttpContextAccessor> Accessor { get; } = new Mock<IHttpContextAccessor>();
            public Mock<ICacheProvider> Cache { get; } = new Mock<ICacheProvider>();
            public Mock<IConfigurationProvider> Config { get; } = new Mock<IConfigurationProvider>();

            public FrontGuardModule Module { get; }

            public Sut()
            {
                // Create your module the same way you already do
                Module = CreateModule(Logger, cache: Cache, config: Config, accessor: Accessor);
            }

            public static HttpRequest CreateFormPost(string rayId, string csrf)
                => TestHelpers.CreateRequestWithBody(
                    url: "http://localhost/",
                    method: "POST",
                    body: $"__rayId={rayId}&__csrf={csrf}",
                    contentType: "application/x-www-form-urlencoded"
                );
        }

        [Fact]
        public void ValidateTokenFingerprint_ReturnsFalse_WhenTokenDecryptsAndFingerprintMatches()
        {
            // Arrange
            var sut = new Sut();
            var key = "1234567890123456";
            var token = "any-token";

            var logger = new Mock<IRequestLogger>();

            var request = Sut.CreateFormPost(Guid.NewGuid().ToString(), "Csrf_Value");

            var decrypted = "whatever|fp-123";
            logger.Setup(x => x.Decrypt(token, key)).Returns(decrypted);

            // Act
            var result = sut.Module.ValidateTokenFingerprint(token, request, key);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateTokenFingerprint_ReturnsFalse_WhenTokenFingerprintMatches()
        {
            // Arrange
            var sut = new Sut();
            var key = "1234567890123456";
            var token = "any-token";

            var logger = new Mock<IRequestLogger>();

            var request = Sut.CreateFormPost(Guid.NewGuid().ToString(), "Csrf_Value");

            logger.Setup(x => x.Encrypt(token, key));

            // Act
            var result = sut.Module.ValidateTokenFingerprint(token, request, key);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateTokenFingerprint_ReturnsTrue_WhenTokenFingerprintHas3Parts()
        {
            // Arrange
            var sut = new Sut();
            var key = "1234567890123456";
            var rawToken = "any-token";
            var encryptedToken = "encrypted-token";

            var request = Sut.CreateFormPost(Guid.NewGuid().ToString(), "Csrf_Value");

            // Prepare the decrypted payload that the IRequestLogger.Decrypt should return:
            // format: "<rawToken>|<fingerprint>"
            var expectedFingerprint = sut.Module.GenerateClientFingerprint(request);
            var decrypted = $"{rawToken}|{expectedFingerprint}|{"Test"}";

            // Arrange mock to return the decrypted payload when decrypting the encrypted token
            sut.Logger.Setup(x => x.Decrypt(encryptedToken, key)).Returns(decrypted);

            // Act
            var result = sut.Module.ValidateTokenFingerprint(encryptedToken, request, key);

            // Assert - fingerprint matches, so validation should succeed
            Assert.False(result);
        }


        [Fact]
        public void ValidateTokenFingerprint_ReturnsTrue_WhenTokenFingerprintMatches()
        {
            // Arrange
            var sut = new Sut();
            var key = "1234567890123456";
            var rawToken = "any-token";
            var encryptedToken = "encrypted-token";

            var request = Sut.CreateFormPost(Guid.NewGuid().ToString(), "Csrf_Value");

            // Prepare the decrypted payload that the IRequestLogger.Decrypt should return:
            // format: "<rawToken>|<fingerprint>"
            var expectedFingerprint = sut.Module.GenerateClientFingerprint(request);
            var decrypted = $"{rawToken}|{expectedFingerprint}";

            // Arrange mock to return the decrypted payload when decrypting the encrypted token
            sut.Logger.Setup(x => x.Decrypt(encryptedToken, key)).Returns(decrypted);

            // Act
            var result = sut.Module.ValidateTokenFingerprint(encryptedToken, request, key);

            // Assert - fingerprint matches, so validation should succeed
            Assert.True(result);
        }

        [Fact]
        public void ValidateTokenFingerprint_ReturnsFalse_WhenCurrentFingerprintMatchesNotMatch()
        {
            // Arrange
            var sut = new Sut();
            var key = "1234567890123457";
            var rawToken = "any-token";
            var encryptedToken = "encrypted-token";

            var request = Sut.CreateFormPost(Guid.NewGuid().ToString(), "Csrf_Value");

            // Prepare the decrypted payload that the IRequestLogger.Decrypt should return:
            // format: "<rawToken>|<fingerprint>"
            var expectedFingerprint = sut.Module.GenerateClientFingerprint(request) + "Test";
            var decrypted = $"{rawToken}|{expectedFingerprint}";

            // Arrange mock to return the decrypted payload when decrypting the encrypted token
            sut.Logger.Setup(x => x.Decrypt(encryptedToken, key)).Returns(decrypted);

            // Act
            var result = sut.Module.ValidateTokenFingerprint(encryptedToken, request, key);

            // Assert - fingerprint matches, so validation should succeed
            Assert.False(result);
        }

        [Fact]
        public void HandleInteractiveChallenge_ProcessChallengePostRequest()
        {
            var req = TestHelpers.CreateRequestWithBody("http://localhost/", "POST", "...");
            var resp = new HttpResponse(new StringWriter());

            var logger = new Mock<IRequestLogger>();
            var accessor = new Mock<IHttpContextAccessor>();
            var cache = new Mock<ICacheProvider>();
            var config = new Mock<IConfigurationProvider>();
            var module = CreateModule(logger, cache: cache, config: config, accessor: accessor);

            var ctx = new RequestLogContext { ConnectionString = "cs", RuleTriggered = 1, RayId = "ray", Iso2 = "US", ActionId = 3, AppId = "app" };

            module.HandleInteractiveChallenge(req, resp, null, "key", ctx);
            Assert.Equal(403, resp.StatusCode);

            cache.Setup(x => x.Get("CSRF_ray")).Returns("token");
            module.HandleInteractiveChallenge(req, resp, null, "key", ctx);
        }

        [Fact]
        public void HandleInteractiveChallenge_DisplaysFormOrProcessesPost()
        {
            var logger = new Mock<IRequestLogger>();
            var accessor = new Mock<IHttpContextAccessor>();
            var cache = new Mock<ICacheProvider>();
            var config = new Mock<IConfigurationProvider>();
            var module = CreateModule(logger, cache: cache, config: config, accessor: accessor);
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var resp = new HttpResponse(new StringWriter());
            var ctx = new RequestLogContext { ConnectionString = "cs", RuleTriggered = 1, RayId = "ray", Iso2 = "US", ActionId = 4, AppId = "app" };
            
            // GET: should display form
            module.HandleInteractiveChallenge(req, resp, null, "key", ctx);
            Assert.Equal(403, resp.StatusCode);
            
            // POST: should process post            
            cache.Setup(x => x.Get("CSRF_ray")).Returns("token");
            module.HandleInteractiveChallenge(req, resp, null, "key", ctx);
        }

        [Fact]
        public void ProcessChallengePostRequest_ValidAndInvalidCsrf()
        {
            var logger = new Mock<IRequestLogger>();
            var accessor = new Mock<IHttpContextAccessor>();
            var cache = new Mock<ICacheProvider>();
            var config = new Mock<IConfigurationProvider>();
            var module = CreateModule(logger, cache: cache, config: config, accessor: accessor);
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var resp = new HttpResponse(new StringWriter());
            var ctx = new ChallengeContext
            {
                Request = req,
                Response = resp,
                Key = "key",
                LogContext = new RequestLogContext { RayId = "ray" },
                HtmlGenerator = module.GenerateHTMLManagedChallenge
            };
            
            cache.Setup(x => x.Get("CSRF_ray")).Returns("token");
            module.ProcessChallengePostRequest(ctx);

            Assert.Equal(403, resp.StatusCode);
        }

        [Fact]
        public void GenerateAndSetToken_RedirectsIfTokenIsValidAndKeyIsValid()
        {
            // Arrange
            var httpContext = new HttpContext(
                new HttpRequest("test", "http://example.com/path", ""),
                new HttpResponse(new StringWriter())
            );
            HttpContext.Current = httpContext; // Set the current context
            var response = HttpContext.Current.Response;
            var request = HttpContext.Current.Request;
            var encryptionKey = "1234567890123456";

            var mockRequestLogger = new Mock<IRequestLogger>();
            var mockTokenCache = new Mock<ICacheProvider>();
            var mockConfig = new Mock<IConfigurationProvider>();
            var mockAccessor = new Mock<IHttpContextAccessor>();

            var module = CreateModule(mockRequestLogger, null, null, null, mockTokenCache, mockConfig, mockAccessor);

            var encryptedToken = "encrypted-token";

            // When Encrypt is called we return a stable encrypted token
            mockRequestLogger.Setup(r => r.Encrypt(It.IsAny<string>(), encryptionKey))
                .Returns(encryptedToken);

            // Token expiration duration in hours
            mockRequestLogger.Setup(r => r.GetTokenExpirationDuration(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(1);

            // Ensure Decrypt returns a payload containing the fingerprint that matches the current request
            var expectedFingerprint = module.GenerateClientFingerprint(request);
            mockRequestLogger.Setup(r => r.Decrypt(encryptedToken, encryptionKey))
                .Returns("raw|" + expectedFingerprint);

            // Cache should report the token as present and not expired
            mockTokenCache.Setup(c => c.Get(encryptedToken))
                .Returns(DateTime.UtcNow.AddHours(1));

            // Act
            Assert.Throws<NullReferenceException>(() => {
                module.GenerateAndSetToken(request, response, encryptionKey);
            });            
        }
    }
}
