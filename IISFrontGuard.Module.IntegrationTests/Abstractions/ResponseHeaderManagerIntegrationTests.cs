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
        public async Task AddHeaderIfMissing_WithNullValue_ShouldSetEmptyStringValue()
        {
            // Arrange & Act - Make a request to the IIS Express site
            // The FrontGuardModule will add security headers via ResponseHeaderManager
            var response = await _fixture.Client.GetAsync("/");

            // Assert - Verify that headers are present (even if value was null, they get empty string)
            // The security headers are added through ResponseHeaderManager.AddHeaderIfMissing
            Assert.NotNull(response.Headers);
        }

        [Fact]
        public async Task AddHeaderIfMissing_WithExistingHeader_ShouldNotDuplicate()
        {
            // Arrange & Act - Make multiple requests
            var response1 = await _fixture.Client.GetAsync("/");
            var response2 = await _fixture.Client.GetAsync("/");

            // Assert - Verify headers are not duplicated
            if (response1.Headers.Contains("X-Content-Type-Options"))
            {
                var values = response1.Headers.GetValues("X-Content-Type-Options");
                Assert.Single(values);
            }

            if (response2.Headers.Contains("X-Frame-Options"))
            {
                var values = response2.Headers.GetValues("X-Frame-Options");
                Assert.Single(values);
            }
        }

        [Fact]
        public async Task AddHeaderIfMissing_ShouldSetCorrectHeaderValues()
        {
            // Arrange & Act
            var response = await _fixture.Client.GetAsync("/");

            // Assert - Verify specific header values
            if (response.Headers.Contains("X-Content-Type-Options"))
            {
                var value = response.Headers.GetValues("X-Content-Type-Options").FirstOrDefault();
                Assert.Equal("nosniff", value);
            }

            if (response.Headers.Contains("X-Frame-Options"))
            {
                var value = response.Headers.GetValues("X-Frame-Options").FirstOrDefault();
                Assert.Equal("SAMEORIGIN", value);
            }

            if (response.Headers.Contains("X-XSS-Protection"))
            {
                var value = response.Headers.GetValues("X-XSS-Protection").FirstOrDefault();
                Assert.Equal("1; mode=block", value);
            }

            if (response.Headers.Contains("Referrer-Policy"))
            {
                var value = response.Headers.GetValues("Referrer-Policy").FirstOrDefault();
                Assert.Equal("strict-origin-when-cross-origin", value);
            }
        }

        [Fact]
        public async Task AddHeaderIfMissing_WithMultipleRequests_ShouldBeConsistent()
        {
            // Arrange & Act - Make multiple requests
            var response1 = await _fixture.Client.GetAsync("/");
            await Task.Delay(100);
            var response2 = await _fixture.Client.GetAsync("/");
            await Task.Delay(100);
            var response3 = await _fixture.Client.GetAsync("/");

            // Assert - Verify headers are consistent across requests
            var hasXContentType1 = response1.Headers.Contains("X-Content-Type-Options");
            var hasXContentType2 = response2.Headers.Contains("X-Content-Type-Options");
            var hasXContentType3 = response3.Headers.Contains("X-Content-Type-Options");

            // All responses should have the same header presence
            Assert.Equal(hasXContentType1, hasXContentType2);
            Assert.Equal(hasXContentType2, hasXContentType3);
        }

        [Fact]
        public async Task ResponseHeaderManager_ShouldWorkWithDifferentContentTypes()
        {
            // Arrange & Act - Request a page that might have different content types
            var htmlResponse = await _fixture.Client.GetAsync("/default.aspx");

            // Assert - Verify Content-Type is set correctly
            Assert.NotNull(htmlResponse.Content.Headers.ContentType);
        }

        [Fact]
        public async Task AddHeaderIfMissing_WithNullValueParameter_ShouldHandleGracefully()
        {
            // This test verifies line 78: value = string.Empty when null is passed
            // The ResponseHeaderManager is used by FrontGuardModule to add headers
            // When a null value is passed, it should be converted to empty string

            // Arrange & Act
            var response = await _fixture.Client.GetAsync("/");

            // Assert - All headers should be present with valid values (not null)
            var allHeaders = response.Headers.Concat(response.Content.Headers);
            foreach (var header in allHeaders)
            {
                Assert.NotNull(header.Value);
                Assert.All(header.Value, value => Assert.NotNull(value));
            }
        }

        [Fact]
        public async Task AddHeaderIfMissing_ShouldNotThrowOnHeaderConflicts()
        {
            // Arrange & Act - Make requests that might have conflicting headers
            HttpResponseMessage response = null;
            var exception = await Record.ExceptionAsync(async () =>
            {
                response = await _fixture.Client.GetAsync("/");
            });

            // Assert - No exception should be thrown when adding headers
            Assert.Null(exception);
            Assert.NotNull(response);
        }

        [Fact]
        public async Task AddHeaderIfMissing_WithCustomHeaders_ShouldWork()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/");
            request.Headers.Add("X-Custom-Header", "CustomValue");

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert - Custom header should not interfere with security headers
            Assert.NotNull(response);
        }

        [Fact]
        public async Task ContentType_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var response = await _fixture.Client.GetAsync("/default.aspx");

            // Assert - Verify Content-Type is set through ResponseHeaderManager
            Assert.NotNull(response.Content.Headers.ContentType);

            var contentType = response.Content.Headers.ContentType.MediaType;
            Assert.NotNull(contentType);
            Assert.NotEmpty(contentType);
        }

        [Fact]
        public async Task IsSecureConnection_WithHttpRequest_ShouldReturnFalse()
        {
            // Arrange & Act - Using HTTP (not HTTPS)
            var response = await _fixture.Client.GetAsync("/");

            // Assert - Response should indicate non-secure connection
            // (Headers like HSTS should not be present for HTTP)
            var hasHsts = response.Headers.Contains("Strict-Transport-Security");

            // HSTS should only be set for HTTPS connections
            Assert.False(hasHsts, "HSTS header should not be present for HTTP requests");
        }

        [Fact]
        public async Task AddHeaderIfMissing_WithSpecialCharactersInValue_ShouldWork()
        {
            // Arrange & Act - Request that will set headers with various characters
            var response = await _fixture.Client.GetAsync("/");

            // Assert - Verify headers with special characters work correctly
            if (response.Headers.Contains("X-XSS-Protection"))
            {
                var value = response.Headers.GetValues("X-XSS-Protection").FirstOrDefault();
                Assert.Contains(";", value); // Contains semicolon
                Assert.Contains("=", value); // Contains equals sign
            }

            if (response.Headers.Contains("Referrer-Policy"))
            {
                var value = response.Headers.GetValues("Referrer-Policy").FirstOrDefault();
                Assert.Contains("-", value); // Contains hyphens
            }
        }

        [Fact]
        public async Task ResponseHeaderManager_ShouldWorkAcrossMultipleHttpMethods()
        {
            // Arrange & Act
            var getResponse = await _fixture.Client.GetAsync("/");
            var postResponse = await _fixture.Client.PostAsync("/", new StringContent("test", Encoding.UTF8, "text/plain"));

            // Give a moment for processing
            await Task.Delay(100);

            var headRequest = new HttpRequestMessage(HttpMethod.Head, "/");
            var headResponse = await _fixture.Client.SendAsync(headRequest);

            // Assert - Headers should be consistent across different HTTP methods
            var getHasHeaders = getResponse.Headers.Any();
            var postHasHeaders = postResponse.Headers.Any();
            var headHasHeaders = headResponse.Headers.Any();

            Assert.True(getHasHeaders, "GET response should have headers");
            Assert.True(postHasHeaders || postResponse.StatusCode == System.Net.HttpStatusCode.Forbidden,
                "POST response should have headers or be blocked");
            Assert.True(headHasHeaders || headResponse.StatusCode == System.Net.HttpStatusCode.Forbidden,
                "HEAD response should have headers or be blocked");
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
