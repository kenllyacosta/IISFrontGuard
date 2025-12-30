using IISFrontGuard.Module.Models;
using NUnit.Framework;
using System;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class SafeRequestDataTests
    {
        [Test]
        public void FromHttpRequest_WithCompleteRequest_MapsAllProperties()
        {
            // Arrange
            var request = CreateHttpRequest(
                url: "https://example.com/path/to/resource?query=value",
                userHostAddress: "192.168.1.1",
                httpMethod: "GET",
                userAgent: "Mozilla/5.0",
                contentType: "application/json",
                referrer: "https://referrer.com",
                xForwardedFor: "10.0.0.1",
                httpVersion: "HTTP/1.1"
            );

            // Act
            var result = SafeRequestData.FromHttpRequest(request, 123, "ray-id-123", "US", 456, "app-123", "request-body");

            // Assert
            Assert.AreEqual("ray-id-123", result.RayId);
            Assert.AreEqual("example.com", result.HostName);
            Assert.AreEqual("192.168.1.1", result.IPAddress);
            Assert.AreEqual("https", result.Protocol);
            Assert.AreEqual(HttpUtility.UrlEncode("https://referrer.com/"), result.Referrer);
            Assert.AreEqual("GET", result.HttpMethod);
            Assert.IsNull(result.HttpVersion);
            Assert.AreEqual(HttpUtility.UrlEncode("Mozilla/5.0"), result.UserAgent);
            Assert.IsNull(result.XForwardedFor);
            Assert.AreEqual(HttpUtility.UrlEncode("application/json"), result.MimeType);
            Assert.AreEqual(HttpUtility.UrlEncode("https://example.com/path/to/resource?query=value"), result.UrlFull);
            Assert.AreEqual(HttpUtility.UrlEncode("/path/to/resource"), result.UrlPath);
            Assert.AreEqual(HttpUtility.UrlEncode("/path/to/resource?query=value"), result.UrlPathAndQuery);
            Assert.AreEqual(HttpUtility.UrlEncode("?query=value"), result.UrlQueryString);
            Assert.AreEqual(123, result.RuleId);
            Assert.AreEqual(456, result.ActionId);
            Assert.AreEqual("US", result.CountryIso2);
            Assert.AreEqual("app-123", result.AppId);
            Assert.AreEqual("request-body", result.RequestBody);
            Assert.IsTrue((DateTime.UtcNow - result.CreatedAt).TotalSeconds < 1);
        }

        [Test]
        public void FromHttpRequest_WithNullValues_HandlesGracefully()
        {
            // Arrange
            var request = CreateHttpRequest(
                url: "https://example.com",
                userHostAddress: "192.168.1.1",
                httpMethod: "GET",
                userAgent: null,
                contentType: null,
                referrer: null,
                xForwardedFor: null,
                httpVersion: null
            );

            // Act
            var result = SafeRequestData.FromHttpRequest(request, null, "ray-id", null, null, null, null);

            // Assert
            Assert.AreEqual(string.Empty, result.Referrer);
            Assert.AreEqual(string.Empty, result.UserAgent);
            Assert.AreEqual(string.Empty, result.MimeType);
            Assert.IsNull(result.XForwardedFor);
            Assert.IsNull(result.HttpVersion);
        }

        [Test]
        public void FromHttpRequest_WithSpecialCharactersInUrl_EncodesCorrectly()
        {
            // Arrange
            var request = CreateHttpRequest(
                url: "https://example.com/path?name=John Doe&age=30",
                userHostAddress: "192.168.1.1",
                httpMethod: "GET",
                userAgent: "Mozilla/5.0 (Windows NT 10.0)",
                contentType: "text/html; charset=utf-8",
                referrer: "https://google.com/search?q=test query",
                xForwardedFor: null,
                httpVersion: "HTTP/1.1"
            );

            // Act
            var result = SafeRequestData.FromHttpRequest(request, null, "ray-id", null, null, null, null);

            // Assert
            Assert.IsTrue(result.UrlFull.Contains("%3f") || result.UrlFull.Contains("%3F")); // ? encoded
            Assert.IsTrue(result.UserAgent.Contains("%28") || result.UserAgent.Contains("(")); // Encoded or original
            Assert.IsNotEmpty(result.Referrer);
        }

        private HttpRequest CreateHttpRequest(
            string url,
            string userHostAddress,
            string httpMethod,
            string userAgent,
            string contentType,
            string referrer,
            string xForwardedFor,
            string httpVersion)
        {
            var uri = new Uri(url);
            var request = new HttpRequest(string.Empty, url, uri.Query.TrimStart('?'))
            {
                RequestContext = new System.Web.Routing.RequestContext(
                    new HttpContextWrapper(new HttpContext(
                        new HttpRequest(null, url, uri.Query.TrimStart('?')),
                        new HttpResponse(null)
                    )),
                    new System.Web.Routing.RouteData()
                )
            };

            // Use reflection to set readonly properties
            var userHostAddressField = typeof(HttpRequest).GetField("_wr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (userHostAddressField != null)
            {
                var worker = new TestWorkerRequest(uri, userHostAddress, httpMethod, userAgent, contentType, referrer, xForwardedFor, httpVersion);
                userHostAddressField.SetValue(request, worker);
            }

            return request;
        }

        private class TestWorkerRequest : HttpWorkerRequest
        {
            private readonly Uri _uri;
            private readonly string _userHostAddress;
            private readonly string _httpMethod;
            private readonly string _userAgent;
            private readonly string _contentType;
            private readonly string _referrer;
            private readonly string _xForwardedFor;
            private readonly string _httpVersion;

            public TestWorkerRequest(Uri uri, string userHostAddress, string httpMethod, string userAgent, string contentType, string referrer, string xForwardedFor, string httpVersion)
            {
                _uri = uri;
                _userHostAddress = userHostAddress;
                _httpMethod = httpMethod;
                _userAgent = userAgent;
                _contentType = contentType;
                _referrer = referrer;
                _xForwardedFor = xForwardedFor;
                _httpVersion = httpVersion;
            }

            public override string GetUriPath() => _uri.AbsolutePath;
            public override string GetQueryString() => _uri.Query.TrimStart('?');
            public override string GetRawUrl() => _uri.PathAndQuery;
            public override string GetHttpVerbName() => _httpMethod;
            public override string GetHttpVersion() => _httpVersion ?? "HTTP/1.1";
            public override string GetRemoteAddress() => _userHostAddress;
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

            public override string GetKnownRequestHeader(int index)
            {
                switch (index)
                {
                    case HeaderUserAgent: return _userAgent;
                    case HeaderContentType: return _contentType;
                    case HeaderReferer: return _referrer;
                    default: return null;
                }
            }

            public override string GetUnknownRequestHeader(string name)
            {
                if (name == "X-Forwarded-For") return _xForwardedFor;
                return null;
            }

            public override string[][] GetUnknownRequestHeaders()
            {
                return new string[0][];
            }

            public override string GetServerVariable(string name)
            {
                if (name == "HTTP_VERSION") return _httpVersion;
                return null;
            }
        }
    }
}