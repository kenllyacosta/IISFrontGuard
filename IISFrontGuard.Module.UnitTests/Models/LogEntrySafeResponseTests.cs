using IISFrontGuard.Module.Models;
using NUnit.Framework;
using System;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class LogEntrySafeResponseTests
    {
        [Test]
        public void FromHttpResponse_WithValidData_MapsAllProperties()
        {
            // Arrange
            var request = CreateHttpRequest("https://example.com/api/test?query=value", "POST");
            var response = CreateHttpResponse(200);
            var rayId = Guid.NewGuid();
            var responseTime = 150L;

            // Act - Covers line 17 (entire object initializer including all properties)
            var result = LogEntrySafeResponse.FromHttpResponse(response, request, rayId, responseTime);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotEqual(Guid.Empty, result.Id);
            Assert.AreEqual(HttpUtility.UrlEncode("https://example.com/api/test?query=value"), result.Url);
            Assert.AreEqual("POST", result.HttpMethod);
            Assert.AreEqual(150L, result.ResponseTime);
            Assert.AreEqual(rayId, result.RayId);
            Assert.AreEqual(200, result.StatusCode);
            Assert.IsTrue((DateTime.UtcNow - result.Timestamp).TotalSeconds < 1);
        }

        [Test]
        public void FromHttpResponse_WithNullRayId_HandlesGracefully()
        {
            // Arrange
            var request = CreateHttpRequest("https://example.com/test", "GET");
            var response = CreateHttpResponse(404);

            // Act - Covers line 17 with null RayId
            var result = LogEntrySafeResponse.FromHttpResponse(response, request, null, 250L);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.RayId);
            Assert.AreEqual(404, result.StatusCode);
        }

        [Test]
        public void FromHttpResponse_WithNullUrl_HandlesGracefully()
        {
            // Arrange
            var request = CreateHttpRequest(null, "DELETE");
            var response = CreateHttpResponse(500);
            var rayId = Guid.NewGuid();

            // Act - Covers line 17 with null URL (SafeUrlEncode handles this)
            var result = LogEntrySafeResponse.FromHttpResponse(response, request, rayId, 1000L);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpUtility.UrlEncode("http://localhost/"), result.Url);
            Assert.AreEqual("DELETE", result.HttpMethod);
        }

        [Test]
        public void FromHttpResponse_WithDifferentStatusCodes_MapsCorrectly()
        {
            // Arrange
            var request = CreateHttpRequest("https://example.com", "PUT");
            var response = CreateHttpResponse(201);

            // Act
            var result = LogEntrySafeResponse.FromHttpResponse(response, request, Guid.NewGuid(), 75L);

            // Assert
            Assert.AreEqual(201, result.StatusCode);
        }

        [Test]
        public void SafeUrlEncode_WithNullValue_ReturnsEmptyString()
        {
            // Act - Covers line 32 (null check path)
            var result = LogEntrySafeResponse.SafeUrlEncode(null);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void SafeUrlEncode_WithEmptyString_ReturnsEmptyString()
        {
            // Act - Covers line 32 (empty string check path)
            var result = LogEntrySafeResponse.SafeUrlEncode(string.Empty);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void SafeUrlEncode_WithValidString_EncodesCorrectly()
        {
            // Arrange
            var input = "https://example.com/path?query=value&test=123";

            // Act - Covers line 32 (encoding path)
            var result = LogEntrySafeResponse.SafeUrlEncode(input);

            // Assert
            Assert.AreEqual(HttpUtility.UrlEncode(input), result);
            Assert.IsTrue(result.Contains("%3a") || result.Contains("%3A")); // : encoded
        }

        [Test]
        public void SafeUrlEncode_WithSpecialCharacters_EncodesCorrectly()
        {
            // Arrange
            var input = "test value with spaces & special chars!";

            // Act - Covers line 32
            var result = LogEntrySafeResponse.SafeUrlEncode(input);

            // Assert
            Assert.AreNotEqual(input, result);
            Assert.IsTrue(result.Contains("+")); // Spaces become +
        }

        [Test]
        public void SafeUrlEncode_WithWhitespaceOnly_ReturnsEncoded()
        {
            // Arrange
            var input = "   ";

            // Act - Covers line 32 (non-empty but whitespace)
            var result = LogEntrySafeResponse.SafeUrlEncode(input);

            // Assert
            Assert.IsNotEmpty(result);
            Assert.AreEqual(HttpUtility.UrlEncode(input), result);
        }

        private HttpRequest CreateHttpRequest(string url, string httpMethod)
        {
            var uri = url != null ? new Uri(url) : null;
            var queryString = uri?.Query.TrimStart('?') ?? string.Empty;
            var request = new HttpRequest(string.Empty, url ?? "http://localhost", queryString)
            {
                RequestContext = new System.Web.Routing.RequestContext(
                    new HttpContextWrapper(new HttpContext(
                        new HttpRequest(null, url ?? "http://localhost", queryString),
                        new HttpResponse(null)
                    )),
                    new System.Web.Routing.RouteData()
                )
            };

            // Use reflection to set HttpMethod
            var httpMethodField = typeof(HttpRequest).GetField("_httpMethod", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            httpMethodField?.SetValue(request, httpMethod);

            // Use TestWorkerRequest for better control
            var workerRequestField = typeof(HttpRequest).GetField("_wr", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (workerRequestField != null && uri != null)
            {
                var worker = new TestWorkerRequest(uri, httpMethod);
                workerRequestField.SetValue(request, worker);
            }

            return request;
        }

        private HttpResponse CreateHttpResponse(int statusCode)
        {
            var response = new HttpResponse(new System.IO.StringWriter())
            {
                StatusCode = statusCode
            };
            return response;
        }

        private class TestWorkerRequest : HttpWorkerRequest
        {
            private readonly Uri _uri;
            private readonly string _httpMethod;

            public TestWorkerRequest(Uri uri, string httpMethod)
            {
                _uri = uri;
                _httpMethod = httpMethod;
            }

            public override string GetUriPath() => _uri?.AbsolutePath ?? "/";
            public override string GetQueryString() => _uri?.Query.TrimStart('?') ?? string.Empty;
            public override string GetRawUrl() => _uri?.PathAndQuery ?? "/";
            public override string GetHttpVerbName() => _httpMethod;
            public override string GetHttpVersion() => "HTTP/1.1";
            public override string GetRemoteAddress() => "127.0.0.1";
            public override int GetRemotePort() => 0;
            public override string GetLocalAddress() => "127.0.0.1";
            public override int GetLocalPort() => 80;
            public override void SendStatus(int statusCode, string statusDescription) { }
            public override void SendKnownResponseHeader(int index, string value) { }
            public override void SendUnknownResponseHeader(string name, string value) { }
            public override void SendResponseFromMemory(byte[] data, int length) { }
            public override void SendResponseFromFile(string filename, long offset, long length) { }
            public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
            public override void FlushResponse(bool finalFlush) { }
            public override void EndOfRequest() { }
        }
    }
}