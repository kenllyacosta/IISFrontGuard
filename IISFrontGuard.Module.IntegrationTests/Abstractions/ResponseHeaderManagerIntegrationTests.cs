using IISFrontGuard.Module.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Abstractions
{
    [Collection("IIS Integration Tests")]
    public class ResponseHeaderManagerIntegrationTests
    {
        private readonly IisIntegrationFixture _fixture;

        public ResponseHeaderManagerIntegrationTests(IisIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void IsSecureConnection_ReturnsFalse_ForHttp()
        {
            var context = new HttpContext(
                new HttpRequest("", "http://localhost/", ""),
                new HttpResponse(new StringWriter())
            );
            var mgr = new ResponseHeaderManager(context.Request, context.Response);

            Assert.NotNull(mgr);
            Assert.False(mgr.IsSecureConnection);
        }

        public sealed class HeaderWriter
        {
            private readonly HttpResponseBase _response;

            public HeaderWriter(HttpResponseBase response)
                => _response = response ?? throw new ArgumentNullException(nameof(response));

            public void AddHeaderIfMissing(string name, string value = "")
            {
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentNullException(nameof(name));

                try
                {
                    if (string.IsNullOrEmpty(_response.Headers[name]))
                    {
                        _response.Headers.Add(name, value);
                    }
                }
                catch (PlatformNotSupportedException)
                {
                    _response.AppendHeader(name, value);
                }
            }
        }
    }
}
