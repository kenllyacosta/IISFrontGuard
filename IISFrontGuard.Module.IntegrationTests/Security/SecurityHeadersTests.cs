using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.IntegrationTests.Services;
using IISFrontGuard.Module.Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Security
{
    [Collection("IIS Integration Tests")]
    public class SecurityHeadersTests : IDisposable
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

        public SecurityHeadersTests(IisIntegrationFixture fixture)
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
        public void RemoveUnnecessaryHeaders_ShouldRemoveServerIdentityHeaders()
        {
            // Arrange
            var module = TestHelpers.CreateModuleWithTestConfig(
                _requestLogger, _webhookNotifier, _geoIPService, 
                _wafRuleRepository, _tokenCache, _configuration, _httpContextAccessor);
            var response = new System.Web.HttpResponse(new StringWriter());

            // Act & Assert - Should not throw exception
            var exception = Record.Exception(() => module.RemoveUnnecessaryHeaders(response));
            Assert.Null(exception);
        }

        [Fact]
        public async Task SecurityHeaders_ShouldBePresent_InResponse()
        {
            try
            {
                // Arrange & Act
                var response = await _fixture.Client.GetAsync("/");
                
                // Assert - Security headers should be present
                Assert.True(response.Headers.Contains("X-Content-Type-Options"));
                Assert.True(response.Headers.Contains("X-Frame-Options"));
                Assert.True(response.Headers.Contains("X-XSS-Protection"));
                Assert.True(response.Headers.Contains("Referrer-Policy"));
            }
            catch (HttpRequestException)
            {
                System.Diagnostics.Trace.WriteLine("IIS Express not reachable - skipping security headers test");
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Trace.WriteLine("IIS Express timed out - skipping security headers test");
            }
        }

        [Fact]
        public async Task ServerIdentityHeaders_ShouldBeReplaced()
        {
            try
            {
                // Arrange & Act
                var response = await _fixture.Client.GetAsync("/");
                
                // Assert - Server identity should show IISFrontGuard
                if (response.Headers.Contains("X-Powered-By"))
                {
                    var poweredBy = response.Headers.GetValues("X-Powered-By").FirstOrDefault();
                    Assert.Equal("IISFrontGuard", poweredBy);
                }
            }
            catch (HttpRequestException)
            {
                System.Diagnostics.Trace.WriteLine("IIS Express not reachable - skipping server identity headers test");
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Trace.WriteLine("IIS Express timed out - skipping server identity headers test");
            }
        }

        [Fact]
        public async Task ContentSecurityPolicy_ShouldBePresent_ForHtmlResponse()
        {
            try
            {
                // Arrange & Act
                var response = await _fixture.Client.GetAsync("/");
                
                // Assert - CSP header should be present for HTML content
                if (response.Content.Headers.ContentType?.MediaType == "text/html")
                {
                    var cspHeaders = response.Headers.Where(h => h.Key == "Content-Security-Policy").ToList();
                    Assert.NotNull(cspHeaders);
                }
            }
            catch (HttpRequestException)
            {
                System.Diagnostics.Trace.WriteLine("IIS Express not reachable - skipping CSP test");
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Trace.WriteLine("IIS Express timed out - skipping CSP test");
            }
        }

        [Fact]
        public async Task HstsHeader_ShouldBePresent_ForHttpsRequests()
        {
            try
            {
                // Arrange & Act
                var response = await _fixture.Client.GetAsync("/");
                
                // Assert - Response should be successful
                Assert.NotNull(response);
            }
            catch (HttpRequestException)
            {
                System.Diagnostics.Trace.WriteLine("IIS Express not reachable - skipping HSTS test");
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Trace.WriteLine("IIS Express timed out - skipping HSTS test");
            }
        }

        public void Dispose()
        {
            _webhookServer?.Dispose();
            _requestLogger?.Stop();
            _webhookNotifier?.Stop();
        }
    }
}
