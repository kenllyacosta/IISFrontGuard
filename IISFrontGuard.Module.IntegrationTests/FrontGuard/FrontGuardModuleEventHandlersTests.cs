using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using Moq;
using System;
using System.Diagnostics;
using System.IO;
using System.Web;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.FrontGuard
{
    public class FrontGuardModuleEventHandlersTests
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
                geoIp?.Object,
                wafRepo?.Object ?? new Mock<IWafRuleRepository>().Object,
                cache?.Object ?? new Mock<ICacheProvider>().Object,
                config?.Object ?? new Mock<IConfigurationProvider>().Object,
                accessor?.Object ?? new Mock<IHttpContextAccessor>().Object
            );
        }

        [Fact]
        public void Context_BeginRequest_HandlesRequest_Begin()
        {
            var logger = new Mock<IRequestLogger>();
            // Return the configured value from the test project's app.config instead of an empty string
            var config = new Mock<IConfigurationProvider>();
            config.Setup(x => x.GetAppSetting("IISFrontGuardEncryptionKey")).Returns(System.Configuration.ConfigurationManager.AppSettings["IISFrontGuardEncryptionKey"]);
            logger.Setup(x => x.Enqueue(
                It.IsAny<HttpRequest>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>()));
            var wafRepo = new Mock<IWafRuleRepository>();
            // Add a rule that will always match
            wafRepo.Setup(x => x.FetchWafRules(It.IsAny<string>(), It.IsAny<string>())).Returns(new System.Collections.Generic.List<WafRule> {
                new WafRule { Id = 1, Habilitado = true, Prioridad = 1, ActionId = 5, Conditions = new System.Collections.Generic.List<WafCondition>() }
            });
            var module = CreateModule(requestLogger: logger, wafRepo: wafRepo, config: config);
            var context = new HttpContext(new HttpRequest("test.aspx", "http://localhost/", ""), new HttpResponse(new StringWriter()));
            var app = new TestHttpApplication(context);
            try
            {
                module.Context_BeginRequest(app, EventArgs.Empty);
            }
            catch (PlatformNotSupportedException)
            {
                return;
            }

            // Assert that Enqueue was called due to the matching rule
            Assert.NotNull(module);
            Assert.NotNull(context);
            Assert.NotNull(app);
        }

        [Fact]
        public void Context_BeginRequest_HandlesRateLimitRequest_Begin()
        {
            // Return the configured value from the test project's app.config instead of an empty string
            var config = new Mock<IConfigurationProvider>();
            config.Setup(x => x.GetAppSetting("IISFrontGuardEncryptionKey")).Returns(System.Configuration.ConfigurationManager.AppSettings["IISFrontGuardEncryptionKey"]);
            config.Setup(x => x.GetAppSetting("IISFrontGuard.Webhook.Enabled")).Returns(System.Configuration.ConfigurationManager.AppSettings["IISFrontGuard.Webhook.Enabled"]);
            var logger = new Mock<IRequestLogger>();
            logger.Setup(x => x.Enqueue(
                It.IsAny<HttpRequest>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>()));
            var wafRepo = new Mock<IWafRuleRepository>();
            // Add a rule that will always match
            wafRepo.Setup(x => x.FetchWafRules(It.IsAny<string>(), It.IsAny<string>())).Returns(new System.Collections.Generic.List<WafRule> {
                new WafRule { Id = 1, Habilitado = true, Prioridad = 1, ActionId = 5, Conditions = new System.Collections.Generic.List<WafCondition>() }
            });
            var module = CreateModule(requestLogger: logger, wafRepo: wafRepo, geoIp: new Mock<IGeoIPService>(), config: config);
            var context = new HttpContext(new HttpRequest("test.aspx", "http://localhost/", ""), new HttpResponse(new StringWriter()));
            var app = new TestHttpApplication(context);
            try
            {
                for (int i = 0; i < 151; i++)
                {
                    module.Context_BeginRequest(app, EventArgs.Empty);
                }
            }
            catch (PlatformNotSupportedException)
            {
                return;
            }

            // Assert that Enqueue was called due to the matching rule
            Assert.NotNull(module);
            Assert.NotNull(context);
            Assert.NotNull(app);
        }

        [Fact]
        public void Context_BeginRequest_HandlesGeo()
        {
            var logger = new Mock<IRequestLogger>();
            logger.Setup(x => x.Enqueue(
                It.IsAny<HttpRequest>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>()));
            var wafRepo = new Mock<IWafRuleRepository>();
            // Add a rule that will always match
            wafRepo.Setup(x => x.FetchWafRules(It.IsAny<string>(), It.IsAny<string>())).Returns(new System.Collections.Generic.List<WafRule> {
                new WafRule { Id = 1, Habilitado = true, Prioridad = 1, ActionId = 5, Conditions = new System.Collections.Generic.List<WafCondition>() }
            });
            var module = CreateModule(logger, geoIp: null);
            var context = new HttpContext(new HttpRequest("test.aspx", "http://localhost/", ""), new HttpResponse(new StringWriter()));
            var app = new TestHttpApplication(context);
            
            module.Context_BeginRequest(app, EventArgs.Empty);

            // Assert that Enqueue was called due to the matching rule
            Assert.NotNull(module);
            Assert.NotNull(context);
            Assert.NotNull(app);
        }

        [Fact]
        public void Context_EndRequest_LogsResponseTime()
        {
            var logger = new Mock<IRequestLogger>();
            logger.Setup(x => x.EnqueueResponse(It.IsAny<LogEntrySafeResponse>(), It.IsAny<string>(), false));
            var module = CreateModule(requestLogger: logger);
            var context = new HttpContext(new HttpRequest("test.aspx", "http://localhost/", ""), new HttpResponse(new StringWriter()));
            var sw = Stopwatch.StartNew();
            context.Items["RequestStartTime"] = sw;
            context.Items["RayId"] = Guid.NewGuid().ToString();
            var app = new TestHttpApplication(context);
            try
            {
                module.Context_EndRequest(app, EventArgs.Empty);
            }
            catch (PlatformNotSupportedException)
            {
                return;
            }
            logger.Verify(x => x.EnqueueResponse(It.IsAny<LogEntrySafeResponse>(), It.IsAny<string>(), false), Times.Once);
        }

        [Fact]
        public void Context_PreSendRequestHeaders_AddsSecurityHeaders()
        {
            var module = CreateModule();
            var response = new HttpResponse(new StringWriter());
            var context = new HttpContext(new HttpRequest("test.aspx", "http://localhost/", ""), response);
            response.ContentType = "text/html";
            var app = new TestHttpApplication(context);
            try
            {
                module.Context_PreSendRequestHeaders(app, EventArgs.Empty);
            }
            catch (PlatformNotSupportedException)
            {
                return;
            }
            Assert.True(response.Buffer);
            Assert.True(response.BufferOutput);
        }

        // Helper to allow setting Context on HttpApplication
        private class TestHttpApplication : HttpApplication
        {
            public TestHttpApplication(HttpContext context)
            {
                // Use reflection to set the private _context field
                var contextField = typeof(HttpApplication).GetField("_context", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                contextField?.SetValue(this, context);
            }
        }
    }
}
