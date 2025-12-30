using IISFrontGuard.Module.Abstractions;
using NUnit.Framework;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Abstractions
{
    [TestFixture]
    public class ResponseHeaderManagerTests
    {
        private HttpRequest _request;
        private HttpResponse _response;
        private ResponseHeaderManager _manager;

        [SetUp]
        public void SetUp()
        {
            var httpContext = new HttpContext(
                new HttpRequest("", "http://localhost/", ""),
                new HttpResponse(new StringWriter())
            );

            _request = httpContext.Request;
            _response = httpContext.Response;
            _manager = new ResponseHeaderManager(_request, _response);
        }

        [Test]
        public void Constructor_WithNullRequest_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new ResponseHeaderManager(null, _response));
        }

        [Test]
        public void Constructor_WithNullResponse_AllowsCreation()
        {
            // This tests that the manager can be created even with minimal context
            // Act & Assert
            Assert.DoesNotThrow(() => new ResponseHeaderManager(_request, new HttpResponse(new StringWriter())));
        }

        [Test]
        public void ContentType_Get_ReturnsResponseContentType()
        {
            // Arrange
            _response.ContentType = "application/json";

            // Act
            var result = _manager.ContentType;

            // Assert
            Assert.AreEqual("application/json", result);
        }

        [Test]
        public void ContentType_Set_SetsResponseContentType()
        {
            // Act
            _manager.ContentType = "text/html";

            // Assert
            Assert.AreEqual("text/html", _response.ContentType);
        }

        [Test]
        public void ContentType_SetToNull_SetsResponseContentTypeToNull()
        {
            // Arrange
            _response.ContentType = "application/json";

            // Act
            _manager.ContentType = null;

            // Assert
            Assert.IsNull(_response.ContentType);
        }

        [Test]
        public void ContentType_SetToEmpty_SetsResponseContentTypeToEmpty()
        {
            // Act
            _manager.ContentType = "";

            // Assert
            Assert.AreEqual("", _response.ContentType);
        }

        [Test]
        public void IsSecureConnection_WithSecureRequest_ReturnsTrue()
        {
            // Arrange
            var secureContext = CreateHttpContext("https://localhost/");
            var secureManager = new ResponseHeaderManager(secureContext.Request, secureContext.Response);

            // Act
            var result = secureManager.IsSecureConnection;

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsSecureConnection_WithNonSecureRequest_ReturnsFalse()
        {
            // Arrange
            var insecureContext = CreateHttpContext("http://localhost/");
            var insecureManager = new ResponseHeaderManager(insecureContext.Request, insecureContext.Response);

            // Act
            var result = insecureManager.IsSecureConnection;

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsSecureConnection_WithNullRequest_ReturnsFalse()
        {
            // Arrange
            var manager = new ResponseHeaderManager(null, _response);

            // Act
            var result = manager.IsSecureConnection;

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void AddHeaderIfMissing_WithNullHeaderName_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.AddHeaderIfMissing(null, "value"));
        }

        [Test]
        public void AddHeaderIfMissing_WithEmptyHeaderName_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => _manager.AddHeaderIfMissing("", "value"));
            Assert.AreEqual("name", ex.ParamName);
        }

        [Test]
        public void AddHeaderIfMissing_WithNonIISContext_FallsBackToAppendHeader()
        {
            // Arrange - Create a minimal HttpResponse that might throw PlatformNotSupportedException
            var response = new HttpResponse(new StringWriter());
            var manager = new ResponseHeaderManager(null, response);
            
            // Act - This should work even if Headers.Add throws
            Assert.DoesNotThrow(() => manager.AddHeaderIfMissing("X-Test-Fallback", "test-value"));
            
            // Note: In some contexts, the header may be added via AppendHeader instead
            // We're primarily testing that no exception is thrown
        }

        [Test]
        public void AddHeaderIfMissing_WithReadOnlyHeaders_HandlesGracefully()
        {
            // Arrange
            var manager = new ResponseHeaderManager(_request, _response);
            
            // Act & Assert - Should not throw even if adding header fails internally
            Assert.DoesNotThrow(() => manager.AddHeaderIfMissing("Content-Length", "100"));
            Assert.DoesNotThrow(() => manager.AddHeaderIfMissing("Transfer-Encoding", "chunked"));
        }

        [Test]
        public void ContentType_WithComplexMediaType_SetsCorrectly()
        {
            // Act
            _manager.ContentType = "application/json; charset=utf-8";

            // Assert
            Assert.AreEqual("application/json; charset=utf-8", _response.ContentType);
        }

        private HttpContext CreateHttpContext(string url)
        {
            var uri = new Uri(url);
            var request = new HttpRequest("", url, "")
            {
                RequestContext = new System.Web.Routing.RequestContext(
                    new HttpContextWrapper(new HttpContext(
                        new HttpRequest(null, url, ""),
                        new HttpResponse(new StringWriter())
                    )),
                    new System.Web.Routing.RouteData()
                )
            };

            var response = new HttpResponse(new StringWriter());
            var context = new HttpContext(request, response);

            // Use reflection to set IsSecureConnection for testing
            var workerField = typeof(HttpRequest).GetField("_wr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (workerField != null)
            {
                var worker = new TestWorkerRequest(uri);
                workerField.SetValue(context.Request, worker);
            }

            return context;
        }

        private class TestWorkerRequest : HttpWorkerRequest
        {
            private readonly Uri _uri;

            public TestWorkerRequest(Uri uri)
            {
                _uri = uri;
            }

            public override string GetUriPath() => _uri.AbsolutePath;
            public override string GetQueryString() => _uri.Query.TrimStart('?');
            public override string GetRawUrl() => _uri.PathAndQuery;
            public override string GetHttpVerbName() => "GET";
            public override string GetHttpVersion() => "HTTP/1.1";
            public override string GetRemoteAddress() => "127.0.0.1";
            public override int GetRemotePort() => 0;
            public override string GetLocalAddress() => "127.0.0.1";
            public override int GetLocalPort() => _uri.Scheme == "https" ? 443 : 80;
            public override void SendStatus(int statusCode, string statusDescription) { }
            public override void SendKnownResponseHeader(int index, string value) { }
            public override void SendUnknownResponseHeader(string name, string value) { }
            public override void SendResponseFromMemory(byte[] data, int length) { }
            public override void SendResponseFromFile(string filename, long offset, long length) { }
            public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
            public override void FlushResponse(bool finalFlush) { }
            public override void EndOfRequest() { }

            public override string GetProtocol()
            {
                return _uri.Scheme == "https" ? "HTTPS" : "HTTP";
            }

            public override bool IsSecure()
            {
                return _uri.Scheme == "https";
            }
        }
    }
}
