using IISFrontGuard.Module.Models;
using MaxMind.GeoIP2.Model;
using MaxMind.GeoIP2.Responses;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Models
{
    [TestFixture]
    public class RequestContextTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithNullRequest_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RequestContext(null, null, null));
        }

        [Test]
        public void Constructor_WithValidRequest_ExtractsCommonValues()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/api/users?id=123",
                "POST",
                clientIp: "192.168.1.1",
                userAgent: "Mozilla/5.0"
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("192.168.1.1", context.ClientIp);
            Assert.AreEqual("Mozilla/5.0", context.UserAgent);
            Assert.AreEqual("/api/users", context.Path);
            Assert.AreEqual("/api/users?id=123", context.PathAndQuery);
            Assert.AreEqual("POST", context.Method);
            Assert.IsNotEmpty(context.Host); // Host will be set but may vary based on HttpContext implementation
            Assert.AreEqual("https", context.Protocol);
            Assert.AreEqual("?id=123", context.QueryString);
            Assert.IsNotEmpty(context.FullUrl); // FullUrl will be set
        }

        [Test]
        public void Constructor_WithHttpRequest_ExtractsHttpProtocol()
        {
            var request = CreateMockHttpRequest(
                "http://example.com/test",
                "GET",
                isSecure: false
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("http", context.Protocol);
        }

        [Test]
        public void Constructor_WithReferrer_ExtractsReferrer()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/page",
                "GET",
                referrer: "https://google.com/search"
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("https://google.com/search", context.Referrer);
        }

        [Test]
        public void Constructor_WithContentType_ExtractsContentType()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/api",
                "POST",
                contentType: "application/json"
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("application/json", context.ContentType);
        }

        [Test]
        public void Constructor_WithGeoInfo_ExtractsGeoData()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");
            
            var geoInfo = CreateMockGeoInfo("US", "United States", "North America");

            var context = new RequestContext(request, geoInfo, null);

            Assert.AreEqual("US", context.CountryIso2);
            Assert.AreEqual("United States", context.CountryName);
            Assert.AreEqual("North America", context.ContinentName);
        }

        [Test]
        public void Constructor_WithNullGeoInfo_SetsEmptyGeoValues()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");

            var context = new RequestContext(request, null, null);

            Assert.AreEqual(string.Empty, context.CountryIso2);
            Assert.AreEqual(string.Empty, context.CountryName);
            Assert.AreEqual(string.Empty, context.ContinentName);
        }

        [Test]
        public void Constructor_WithXForwardedFor_ExtractsXForwardedFor()
        {
            var headers = new Dictionary<string, string>
            {
                { "X-Forwarded-For", "203.0.113.1, 198.51.100.1" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("203.0.113.1, 198.51.100.1", context.XForwardedFor);
        }

        [Test]
        public void Constructor_WithBodyStream_ExtractsBodyLength()
        {
            var bodyBytes = Encoding.UTF8.GetBytes("test body content");
            var request = CreateMockHttpRequestWithBody(
                "https://example.com/api",
                "POST",
                bodyBytes
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual(bodyBytes.Length, context.BodyLength);
        }

        [Test]
        public void Constructor_WithEmptyPath_HandlesGracefully()
        {
            var request = CreateMockHttpRequest("https://example.com/", "POST");

            var context = new RequestContext(request, null, null);

            Assert.IsNotNull(context);
            Assert.AreEqual("/", context.Path);
        }

        #endregion

        #region GetClientIpInternal Tests (via Constructor)

        [Test]
        public void Constructor_WithCloudflareHeader_PrefersCloudflareIp()
        {
            var headers = new Dictionary<string, string>
            {
                { "CF-Connecting-IP", "1.2.3.4" },
                { "X-Forwarded-For", "5.6.7.8" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                clientIp: "127.0.0.1",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("1.2.3.4", context.ClientIp);
        }

        [Test]
        public void Constructor_WithXForwardedForHeader_ExtractsFirstIp()
        {
            var headers = new Dictionary<string, string>
            {
                { "X-Forwarded-For", "203.0.113.1, 198.51.100.1, 192.168.1.1" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                clientIp: "127.0.0.1",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("203.0.113.1", context.ClientIp);
        }

        [Test]
        public void Constructor_WithNoProxyHeaders_UsesDirectIp()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                clientIp: "192.168.1.100"
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("192.168.1.100", context.ClientIp);
        }

        [Test]
        public void Constructor_WithEmptyXForwardedFor_UsesDirectIp()
        {
            var headers = new Dictionary<string, string>
            {
                { "X-Forwarded-For", "" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                clientIp: "10.0.0.1",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("10.0.0.1", context.ClientIp);
        }

        #endregion

        #region GetMimeTypeInternal Tests (via Constructor)

        [Test]
        public void Constructor_WithContentType_ExtractsMimeType()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/api",
                "POST",
                contentType: "application/json; charset=utf-8"
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("application/json", context.MimeType);
        }

        [Test]
        public void Constructor_WithSimpleContentType_ExtractsMimeType()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/api",
                "POST",
                contentType: "text/plain"
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("text/plain", context.MimeType);
        }

        [Test]
        public void Constructor_WithFileExtension_InfersMimeType()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/image.png",
                "GET"
            );

            var context = new RequestContext(request, null, null);

            Assert.That(context.MimeType, Does.Contain("image"));
        }

        [Test]
        public void Constructor_WithoutContentTypeOrExtension_ReturnsEmptyMimeType()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/api",
                "GET"
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual(string.Empty, context.MimeType);
        }

        #endregion

        #region GetCookie Tests

        [Test]
        public void GetCookie_WithExistingCookie_ReturnsValue()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");
            AddCookie(request, "SessionId", "abc123");
            AddCookie(request, "UserId", "user456");

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("abc123", context.GetCookie("SessionId"));
            Assert.AreEqual("user456", context.GetCookie("UserId"));
        }

        [Test]
        public void GetCookie_WithNonExistingCookie_ReturnsEmpty()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");

            var context = new RequestContext(request, null, null);

            Assert.AreEqual(string.Empty, context.GetCookie("NonExistent"));
        }

        [Test]
        public void GetCookie_CaseInsensitive_ReturnsValue()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");
            AddCookie(request, "SessionId", "abc123");

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("abc123", context.GetCookie("sessionid"));
            Assert.AreEqual("abc123", context.GetCookie("SESSIONID"));
        }

        [Test]
        public void GetCookie_CalledMultipleTimes_UsesCachedValues()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");
            AddCookie(request, "Test", "value");

            var context = new RequestContext(request, null, null);

            var value1 = context.GetCookie("Test");
            var value2 = context.GetCookie("Test");

            Assert.AreEqual(value1, value2);
            Assert.AreEqual("value", value1);
        }

        [Test]
        public void GetCookie_WithEmptyCookieName_ReturnsEmpty()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");
            var context = new RequestContext(request, null, null);

            Assert.AreEqual(string.Empty, context.GetCookie(string.Empty));
        }

        [Test]
        public void GetCookie_WithNullCookieValue_ReturnsEmpty()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");
            AddCookie(request, "NullValue", null);

            var context = new RequestContext(request, null, null);

            Assert.AreEqual(string.Empty, context.GetCookie("NullValue"));
        }

        #endregion

        #region GetHeader Tests

        [Test]
        public void GetHeader_WithExistingHeader_ReturnsValue()
        {
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token123" },
                { "Accept", "application/json" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("Bearer token123", context.GetHeader("Authorization"));
            Assert.AreEqual("application/json", context.GetHeader("Accept"));
        }

        [Test]
        public void GetHeader_WithNonExistingHeader_ReturnsEmpty()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");

            var context = new RequestContext(request, null, null);

            Assert.AreEqual(string.Empty, context.GetHeader("NonExistent"));
        }

        [Test]
        public void GetHeader_CaseInsensitive_ReturnsValue()
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("application/json", context.GetHeader("content-type"));
            Assert.AreEqual("application/json", context.GetHeader("CONTENT-TYPE"));
        }

        [Test]
        public void GetHeader_CalledMultipleTimes_UsesCachedValues()
        {
            var headers = new Dictionary<string, string>
            {
                { "Test-Header", "test-value" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            var value1 = context.GetHeader("Test-Header");
            var value2 = context.GetHeader("Test-Header");

            Assert.AreEqual(value1, value2);
            Assert.AreEqual("test-value", value1);
        }

        [Test]
        public void GetHeader_WithNullHeaderValue_ReturnsEmpty()
        {
            var headers = new Dictionary<string, string>
            {
                { "Null-Header", null }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual(string.Empty, context.GetHeader("Null-Header"));
        }

        #endregion

        #region GetBody Tests

        [Test]
        public void GetBody_WithBodyExtractor_ReturnsBody()
        {
            var request = CreateMockHttpRequest("https://example.com/api", "POST");
            var expectedBody = "{\"name\":\"test\"}";

            Func<HttpRequest, string> bodyExtractor = (req) => expectedBody;

            var context = new RequestContext(request, null, bodyExtractor);

            Assert.AreEqual(expectedBody, context.GetBody());
        }

        [Test]
        public void GetBody_CalledMultipleTimes_ExtractsOnce()
        {
            var request = CreateMockHttpRequest("https://example.com/api", "POST");
            var callCount = 0;

            Func<HttpRequest, string> bodyExtractor = (req) =>
            {
                callCount++;
                return "body content";
            };

            var context = new RequestContext(request, null, bodyExtractor);

            var body1 = context.GetBody();
            var body2 = context.GetBody();
            var body3 = context.GetBody();

            Assert.AreEqual("body content", body1);
            Assert.AreEqual("body content", body2);
            Assert.AreEqual("body content", body3);
            Assert.AreEqual(1, callCount, "Body should only be extracted once");
        }

        [Test]
        public void GetBody_WithNullExtractor_ReturnsEmpty()
        {
            var request = CreateMockHttpRequest("https://example.com/api", "POST");

            var context = new RequestContext(request, null, null);

            Assert.AreEqual(string.Empty, context.GetBody());
        }

        [Test]
        public void GetBody_WithExtractorReturningNull_ReturnsEmpty()
        {
            var request = CreateMockHttpRequest("https://example.com/api", "POST");

            Func<HttpRequest, string> bodyExtractor = (req) => null;

            var context = new RequestContext(request, null, bodyExtractor);

            Assert.AreEqual(string.Empty, context.GetBody());
        }

        #endregion

        #region GetClientIpFromHeader Tests

        [Test]
        public void GetClientIpFromHeader_WithExistingHeader_ReturnsHeaderValue()
        {
            var headers = new Dictionary<string, string>
            {
                { "X-Real-IP", "10.20.30.40" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                clientIp: "192.168.1.1",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("10.20.30.40", context.GetClientIpFromHeader("X-Real-IP"));
        }

        [Test]
        public void GetClientIpFromHeader_WithNonExistingHeader_ReturnsClientIp()
        {
            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                clientIp: "192.168.1.1"
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("192.168.1.1", context.GetClientIpFromHeader("X-Real-IP"));
        }

        [Test]
        public void GetClientIpFromHeader_WithEmptyHeader_ReturnsClientIp()
        {
            var headers = new Dictionary<string, string>
            {
                { "X-Real-IP", "" }
            };

            var request = CreateMockHttpRequest(
                "https://example.com/",
                "GET",
                clientIp: "192.168.1.1",
                headers: headers
            );

            var context = new RequestContext(request, null, null);

            Assert.AreEqual("192.168.1.1", context.GetClientIpFromHeader("X-Real-IP"));
        }

        #endregion

        #region Edge Cases and Integration Tests

        [Test]
        public void Integration_CompleteRequest_AllPropertiesPopulated()
        {
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token" },
                { "X-Forwarded-For", "1.2.3.4" }
            };

            var request = CreateMockHttpRequest(
                "https://api.example.com/v1/users?page=1",
                "POST",
                clientIp: "127.0.0.1",
                userAgent: "TestAgent/1.0",
                contentType: "application/json; charset=utf-8",
                referrer: "https://example.com/home",
                headers: headers
            );

            AddCookie(request, "session", "xyz789");

            var geoInfo = CreateMockGeoInfo("GB", "United Kingdom", "Europe");

            Func<HttpRequest, string> bodyExtractor = (req) => "{\"test\":\"data\"}";

            var context = new RequestContext(request, geoInfo, bodyExtractor);

            Assert.AreEqual("1.2.3.4", context.ClientIp);
            Assert.AreEqual("TestAgent/1.0", context.UserAgent);
            Assert.AreEqual("/v1/users", context.Path);
            Assert.AreEqual("/v1/users?page=1", context.PathAndQuery);
            Assert.AreEqual("POST", context.Method);
            Assert.IsNotEmpty(context.Host); // Host will be set
            Assert.AreEqual("https", context.Protocol);
            Assert.AreEqual("?page=1", context.QueryString);
            Assert.That(context.ContentType, Does.Contain("application/json")); // May include charset
            Assert.AreEqual("GB", context.CountryIso2);
            Assert.AreEqual("United Kingdom", context.CountryName);
            Assert.AreEqual("Europe", context.ContinentName);
            Assert.AreEqual("application/json", context.MimeType);
            Assert.AreEqual("Bearer token", context.GetHeader("Authorization"));
            Assert.AreEqual("xyz789", context.GetCookie("session"));
            Assert.AreEqual("{\"test\":\"data\"}", context.GetBody());
        }

        [Test]
        public void EdgeCase_MinimalRequest_HandlesGracefully()
        {
            var request = CreateMockHttpRequest("https://example.com/", "GET");

            var context = new RequestContext(request, null, null);

            Assert.IsNotNull(context);
            Assert.AreEqual(string.Empty, context.GetCookie("any"));
            Assert.AreEqual(string.Empty, context.GetHeader("any"));
            Assert.AreEqual(string.Empty, context.GetBody());
        }

        #endregion

        #region Helper Methods

        private HttpRequest CreateMockHttpRequest(
            string url,
            string method,
            string clientIp = "127.0.0.1",
            string userAgent = "TestAgent",
            string contentType = null,
            string referrer = null,
            bool isSecure = true,
            Dictionary<string, string> headers = null)
        {
            var uri = new Uri(url);
            var workerRequest = new TestHttpWorkerRequest(uri, method, clientIp, userAgent, isSecure);

            if (!string.IsNullOrEmpty(contentType))
            {
                workerRequest.SetContentType(contentType);
            }

            if (!string.IsNullOrEmpty(referrer))
            {
                workerRequest.SetReferrer(referrer);
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    workerRequest.AddHeader(header.Key, header.Value);
                }
            }

            var context = new HttpContext(workerRequest);
            HttpContext.Current = context;

            return context.Request;
        }

        private HttpRequest CreateMockHttpRequestWithBody(string url, string method, byte[] bodyBytes)
        {
            var uri = new Uri(url);
            var workerRequest = new TestHttpWorkerRequest(uri, method, "127.0.0.1", "TestAgent", true);
            workerRequest.SetBody(bodyBytes);

            var context = new HttpContext(workerRequest);
            HttpContext.Current = context;

            return context.Request;
        }

        private void AddCookie(HttpRequest request, string name, string value)
        {
            var cookie = new HttpCookie(name, value);
            request.Cookies.Add(cookie);
        }

        private CountryResponse CreateMockGeoInfo(string isoCode, string countryName, string continentName)
        {
            var continent = new Continent(
                code: isoCode.Substring(0, 2),
                geoNameId: 1,
                names: new Dictionary<string, string> { { "en", continentName } }
            );

            var country = new Country(
                geoNameId: 1,
                isInEuropeanUnion: false,
                isoCode: isoCode,
                names: new Dictionary<string, string> { { "en", countryName } }
            );

            return new CountryResponse(
                continent: continent,
                country: country,
                registeredCountry: country,
                representedCountry: null,
                traits: null
            );
        }

        #endregion

        #region Test HttpWorkerRequest Implementation

        private class TestHttpWorkerRequest : HttpWorkerRequest
        {
            private readonly Uri _uri;
            private readonly string _method;
            private readonly string _clientIp;
            private readonly string _userAgent;
            private readonly bool _isSecure;
            private readonly Dictionary<string, string> _headers;
            private string _contentType;
            private string _referrer;
            private byte[] _bodyBytes;

            public TestHttpWorkerRequest(Uri uri, string method, string clientIp, string userAgent, bool isSecure)
            {
                _uri = uri;
                _method = method;
                _clientIp = clientIp;
                _userAgent = userAgent;
                _isSecure = isSecure;
                _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            public void AddHeader(string name, string value)
            {
                _headers[name] = value;
            }

            public void SetContentType(string contentType)
            {
                _contentType = contentType;
            }

            public void SetReferrer(string referrer)
            {
                _referrer = referrer;
            }

            public void SetBody(byte[] bodyBytes)
            {
                _bodyBytes = bodyBytes;
            }

            public override string GetUriPath() => _uri.AbsolutePath;
            public override string GetQueryString() => _uri.Query.TrimStart('?');
            public override string GetRawUrl() => _uri.PathAndQuery;
            public override string GetHttpVerbName() => _method;
            public override string GetHttpVersion() => "HTTP/1.1";
            public override string GetRemoteAddress() => _clientIp;
            public override int GetRemotePort() => 80;
            public override string GetLocalAddress() => "127.0.0.1";
            public override int GetLocalPort() => _isSecure ? 443 : 80;
            public override string GetProtocol() => _isSecure ? "https" : "http";
            public override bool IsSecure() => _isSecure;

            public override string GetServerVariable(string name)
            {
                if (name == "REMOTE_ADDR") return _clientIp;
                if (name == "HTTP_USER_AGENT") return _userAgent;
                if (name == "SERVER_PROTOCOL") return "HTTP/1.1";
                if (name == "CONTENT_TYPE") return _contentType;
                if (name == "HTTP_HOST") return _uri.Host;
                if (name == "SERVER_NAME") return _uri.Host;
                return string.Empty;
            }

            public override string GetKnownRequestHeader(int index)
            {
                var headerName = GetKnownRequestHeaderName(index);
                if (!string.IsNullOrEmpty(headerName) && _headers.ContainsKey(headerName))
                {
                    return _headers[headerName];
                }

                if (index == HeaderUserAgent)
                {
                    return _userAgent;
                }

                if (index == HeaderContentType && !string.IsNullOrEmpty(_contentType))
                {
                    return _contentType;
                }

                if (index == HeaderReferer && !string.IsNullOrEmpty(_referrer))
                {
                    return _referrer;
                }

                return null;
            }

            public override string GetUnknownRequestHeader(string name)
            {
                if (_headers.ContainsKey(name))
                {
                    return _headers[name];
                }
                return null;
            }

            public override string[][] GetUnknownRequestHeaders()
            {
                var headers = new List<string[]>();
                foreach (var kvp in _headers)
                {
                    headers.Add(new[] { kvp.Key, kvp.Value });
                }
                return headers.ToArray();
            }

            public override byte[] GetPreloadedEntityBody()
            {
                return _bodyBytes;
            }

            public override int GetPreloadedEntityBodyLength()
            {
                return _bodyBytes?.Length ?? 0;
            }

            public override int ReadEntityBody(byte[] buffer, int size)
            {
                return 0;
            }

            public override void SendStatus(int statusCode, string statusDescription) { }
            public override void SendKnownResponseHeader(int index, string value) { }
            public override void SendUnknownResponseHeader(string name, string value) { }
            public override void SendResponseFromMemory(byte[] data, int length) { }
            public override void SendResponseFromFile(string filename, long offset, long length) { }
            public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
            public override void FlushResponse(bool finalFlush) { }
            public override void EndOfRequest() { }
        }

        #endregion
    }
}
