using IISFrontGuard.Module.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;

namespace IISFrontGuard.Module.IntegrationTests.Helpers
{
    public static class TestHelpers
    {
        public static FrontGuardModule CreateModuleWithTestConfig(IRequestLogger requestLogger, IWebhookNotifier webhookNotifier, IGeoIPService geoIPService, IWafRuleRepository wafRuleRepository, ICacheProvider tokenCache, IConfigurationProvider configuration, IHttpContextAccessor httpContextAccessor)
        {
            return new FrontGuardModule(
                requestLogger,
                webhookNotifier,
                geoIPService,
                wafRuleRepository,
                tokenCache,
                configuration,
                httpContextAccessor
            );
        }

        public static HttpRequest CreateMockHttpRequest(string url, string method, string clientIp = "127.0.0.1", string userAgent = "TestAgent")
        {
            return CreateMockHttpRequestWithHeaders(url, method, clientIp, userAgent, null);
        }

        public static HttpRequest CreateMockHttpRequestWithHeaders(string url, string method, string clientIp = "127.0.0.1", string userAgent = "TestAgent", Dictionary<string, string> headers = null)
        {
            var uri = new Uri(url);
            var workerRequest = new TestHttpWorkerRequest(uri, method, clientIp, userAgent);
            
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    workerRequest.AddHeader(header.Key, header.Value);
                }
            }
            
            var request = new HttpRequest("", url, uri.Query.TrimStart('?'))
            {
                RequestContext = new System.Web.Routing.RequestContext()
            };

            typeof(HttpRequest).GetField("_wr", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(request, workerRequest);

            return request;
        }

        public static HttpRequest CreateRequestWithBody(string url, string method, string body, string contentType = "application/json", string clientIp = "127.0.0.1", string userAgent = "TestAgent")
        {
            var bodyBytes = body == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
            return CreateRequestWithBody(url, method, bodyBytes, contentType, clientIp, userAgent);
        }

        public static HttpRequest CreateRequestWithBody(string url, string method, byte[] bodyBytes, string contentType = "application/octet-stream", string clientIp = "127.0.0.1", string userAgent = "TestAgent")
        {
            var uri = new Uri(url);

            var workerRequest = new TestHttpWorkerRequestBody(
                uri: uri,
                method: method,
                clientIp: clientIp,
                userAgent: userAgent,
                body: bodyBytes,
                contentType: contentType
            );

            // This is the key: HttpContext builds HttpRequest from HttpWorkerRequest,
            // so HttpMethod will be POST correctly.
            var context = new HttpContext(workerRequest);

            // Optional if your code reads HttpContext.Current
            HttpContext.Current = context;

            // Optional if you need Routing context
            context.Request.RequestContext = new System.Web.Routing.RequestContext();

            return context.Request;
        }

        public static HttpResponse CreateMockHttpResponseWithHeaders()
        {
            var workerResponse = new TestHttpWorkerResponse();
            var response = new HttpResponse(new StringWriter());

            typeof(HttpResponse).GetField("_wr", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(response, workerResponse);

            return response;
        }

        private class TestHttpWorkerRequest : HttpWorkerRequest
        {
            private readonly Uri _uri;
            private readonly string _method;
            private readonly string _clientIp;
            private readonly string _userAgent;
            private readonly Dictionary<string, string> _headers;

            private byte[] _bodyBytes;

            public TestHttpWorkerRequest(Uri uri, string method, string clientIp, string userAgent)
            {
                _uri = uri;
                _method = method;
                _clientIp = clientIp;
                _userAgent = userAgent;
                _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            public void AddHeader(string name, string value)
            {
                _headers[name] = value;
            }

            public void SetBody(string body)
            {
                _bodyBytes = Encoding.UTF8.GetBytes(body);
            }

            public override string GetUriPath() => _uri.AbsolutePath;
            public override string GetQueryString() => _uri.Query.TrimStart('?');
            public override string GetRawUrl() => _uri.PathAndQuery;
            public override string GetHttpVerbName() => _method;
            public override string GetHttpVersion() => "HTTP/1.1";
            public override string GetRemoteAddress() => _clientIp;
            public override int GetRemotePort() => 80;
            public override string GetLocalAddress() => "127.0.0.1";
            public override int GetLocalPort() => _uri.Port;
            
            public override string GetServerVariable(string name)
            {
                if (name == "REMOTE_ADDR") return _clientIp;
                if (name == "HTTP_USER_AGENT") return _userAgent;
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

            public override void SendStatus(int statusCode, string statusDescription) { }
            public override void SendKnownResponseHeader(int index, string value) { }
            public override void SendUnknownResponseHeader(string name, string value) { }
            public override void SendResponseFromMemory(byte[] data, int length) { }
            public override void SendResponseFromFile(string filename, long offset, long length) { }
            public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
            public override void FlushResponse(bool finalFlush) { }
            public override void EndOfRequest() { }

            public override byte[] GetPreloadedEntityBody()
            {
                return _bodyBytes;
            }

            public override int ReadEntityBody(byte[] buffer, int size)
            {
                return 0; // All data is preloaded
            }

            public override int GetPreloadedEntityBodyLength()
            {
                return _bodyBytes?.Length ?? 0;
            }

            public override int GetTotalEntityBodyLength()
            {
                return _bodyBytes?.Length ?? 0;
            }

            public override bool IsEntireEntityBodyIsPreloaded()
            {
                return true;
            }
        }

        public class TestHttpWorkerRequestBody : HttpWorkerRequest
        {
            private readonly Uri _uri;
            private readonly string _method;
            private readonly string _clientIp;
            private readonly string _userAgent;
            private readonly string _contentType;
            private readonly byte[] _body;

            private readonly int _preloadedLength;
            private int _readOffset;

            public TestHttpWorkerRequestBody(
                Uri uri,
                string method,
                string clientIp,
                string userAgent,
                byte[] body,
                string contentType,
                int? preloadBytes = null) // null => preload ALL (no 1024 cap)
            {
                _uri = uri;
                _method = method ?? "POST";
                _clientIp = clientIp ?? "127.0.0.1";
                _userAgent = userAgent ?? "TestAgent";
                _body = body ?? Array.Empty<byte>();
                _contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

                int requested = preloadBytes ?? _body.Length;
                if (requested < 0) requested = 0;
                if (requested > _body.Length) requested = _body.Length;

                _preloadedLength = requested;

                // IMPORTANT: ReadEntityBody must start AFTER the preloaded bytes,
                // regardless of call order.
                _readOffset = _preloadedLength;
            }

            // ---- Entity body plumbing (no limit) ----

            public override byte[] GetPreloadedEntityBody()
            {
                _readOffset = _body.Length;   // everything already "consumed" as preloaded
                return _body;                 // return full body
            }

            public override int GetPreloadedEntityBodyLength()
            {
                return _body.Length;
            }

            public override bool IsEntireEntityBodyIsPreloaded()
            {
                return true;
            }

            public override int ReadEntityBody(byte[] buffer, int size)
            {
                // No remaining body because we preloaded it all
                return 0;
            }

            // ---- Minimal request info ----
            public override string GetHttpVerbName() => _method;
            public override string GetRawUrl() => _uri.PathAndQuery;
            public override string GetUriPath() => _uri.AbsolutePath;
            public override string GetQueryString() => _uri.Query.TrimStart('?');
            public override string GetHttpVersion() => "HTTP/1.1";

            public override string GetRemoteAddress() => _clientIp;
            public override string GetRemoteName() => _clientIp;
            public override int GetRemotePort() => 12345;

            public override string GetLocalAddress() => "127.0.0.1";
            public override int GetLocalPort() => 80;

            public override string GetKnownRequestHeader(int index)
            {
                if (index == HeaderContentType) return _contentType;
                if (index == HeaderContentLength) return _body.Length.ToString();
                if (index == HeaderUserAgent) return _userAgent;
                return null;
            }

            public override string GetUnknownRequestHeader(string name) => null;
            public override string[][] GetUnknownRequestHeaders() => Array.Empty<string[]>();

            // ---- Entity body plumbing ----

            public override int GetTotalEntityBodyLength() => _body.Length;

            // ---- Response methods ----
            public override void SendStatus(int statusCode, string statusDescription) { }
            public override void SendKnownResponseHeader(int index, string value) { }
            public override void SendUnknownResponseHeader(string name, string value) { }
            public override void SendResponseFromMemory(byte[] data, int length) { }
            public override void SendResponseFromFile(string filename, long offset, long length) { }
            public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
            public override void FlushResponse(bool finalFlush) { }
            public override void EndOfRequest() { }
        }

        //Response
        public sealed class TestHttpWorkerResponse : HttpWorkerRequest
        {
            private readonly MemoryStream _body = new MemoryStream();
            private readonly Dictionary<string, string> _headers =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public int StatusCode { get; private set; } = 200;
            public string StatusDescription { get; private set; } = "OK";

            public IReadOnlyDictionary<string, string> Headers => _headers;
            public byte[] BodyBytes => _body.ToArray();
            public string BodyText => Encoding.UTF8.GetString(BodyBytes);

            // -------- Response capture --------

            public override void SendStatus(int statusCode, string statusDescription)
            {
                StatusCode = statusCode;
                StatusDescription = statusDescription;
            }

            public override void SendKnownResponseHeader(int index, string value)
            {
                string name = GetKnownResponseHeaderName(index);
                if (name != null)
                    _headers[name] = value;
            }

            public override void SendUnknownResponseHeader(string name, string value)
            {
                _headers[name] = value;
            }

            public override void SendResponseFromMemory(byte[] data, int length)
            {
                _body.Write(data, 0, length);
            }

            public override void SendResponseFromFile(string filename, long offset, long length)
            {
                using (var fs = File.OpenRead(filename))
                {
                    fs.Seek(offset, SeekOrigin.Begin);

                    byte[] buffer = new byte[8192];
                    long remaining = length;

                    while (remaining > 0)
                    {
                        int read = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (read == 0) break;

                        _body.Write(buffer, 0, read);
                        remaining -= read;
                    }
                }
            }

            public override void SendResponseFromFile(IntPtr handle, long offset, long length)
                => throw new NotSupportedException();

            public override void FlushResponse(bool finalFlush) { }

            public override void EndOfRequest() { }

            // -------- Minimal required request stubs --------
            // (ASP.NET requires these even if unused)

            public override string GetHttpVerbName() => "GET";
            public override string GetRawUrl() => "/";
            public override string GetUriPath() => "/";
            public override string GetHttpVersion() => "HTTP/1.1";
            public override string GetRemoteAddress() => "127.0.0.1";
            public override string GetLocalAddress() => "127.0.0.1";
            public override int GetRemotePort() => 12345;
            public override int GetLocalPort() => 80;

            public override string GetKnownRequestHeader(int index) => null;
            public override string GetUnknownRequestHeader(string name) => null;
            public override string[][] GetUnknownRequestHeaders() => Array.Empty<string[]>();

            public override byte[] GetPreloadedEntityBody() => Array.Empty<byte>();
            public override int GetPreloadedEntityBodyLength() => 0;
            public override int GetTotalEntityBodyLength() => 0;
            public override int ReadEntityBody(byte[] buffer, int size) => 0;
            public override bool IsEntireEntityBodyIsPreloaded() => true;

            public override string GetQueryString()
                => throw new NotSupportedException();
        }

        public sealed class TestHttpWorkerRequestWithResponse : HttpWorkerRequest
        {
            // ----- Request -----
            private readonly Uri _uri;
            private readonly string _method;
            private readonly string _clientIp;
            private readonly string _userAgent;
            private readonly string _contentType;
            private readonly byte[] _body;

            public TestHttpWorkerRequestWithResponse(
                Uri uri,
                string method = "GET",
                string clientIp = "127.0.0.1",
                string userAgent = "TestAgent",
                byte[] body = null,
                string contentType = "application/octet-stream")
            {
                _uri = uri ?? throw new ArgumentNullException(nameof(uri));
                _method = method ?? "GET";
                _clientIp = clientIp ?? "127.0.0.1";
                _userAgent = userAgent ?? "TestAgent";
                _body = body ?? Array.Empty<byte>();
                _contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            }

            public override string GetHttpVerbName() => _method;
            public override string GetRawUrl() => _uri.PathAndQuery;
            public override string GetUriPath() => _uri.AbsolutePath;
            public override string GetQueryString() => _uri.Query.TrimStart('?');
            public override string GetHttpVersion() => "HTTP/1.1";

            public override string GetRemoteAddress() => _clientIp;
            public override string GetRemoteName() => _clientIp;
            public override int GetRemotePort() => 12345;

            public override string GetLocalAddress() => "127.0.0.1";
            public override int GetLocalPort() => 80;

            public override int GetTotalEntityBodyLength() => _body.Length;
            public override byte[] GetPreloadedEntityBody() => _body;
            public override int GetPreloadedEntityBodyLength() => _body.Length;
            public override bool IsEntireEntityBodyIsPreloaded() => true;
            public override int ReadEntityBody(byte[] buffer, int size) => 0;

            public override string GetKnownRequestHeader(int index)
            {
                if (index == HeaderContentType) return _contentType;
                if (index == HeaderContentLength) return _body.Length.ToString();
                if (index == HeaderUserAgent) return _userAgent;
                return null;
            }

            public override string GetUnknownRequestHeader(string name) => null;
            public override string[][] GetUnknownRequestHeaders() => Array.Empty<string[]>();

            // ----- Captured Response -----
            public int StatusCode { get; private set; } = 200;
            public string StatusDescription { get; private set; } = "OK";

            // case-insensitive header storage
            public Dictionary<string, string> ResponseHeaders { get; } =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            private readonly MemoryStream _responseBody = new MemoryStream();
            public byte[] ResponseBodyBytes => _responseBody.ToArray();
            public string ResponseBodyText => Encoding.UTF8.GetString(ResponseBodyBytes);

            public bool Ended { get; private set; }

            public override void SendStatus(int statusCode, string statusDescription)
            {
                StatusCode = statusCode;
                StatusDescription = statusDescription;
            }

            public override void SendKnownResponseHeader(int index, string value)
            {
                // maps known header indices to names
                var name = HttpWorkerRequest.GetKnownResponseHeaderName(index);
                if (!string.IsNullOrEmpty(name))
                    ResponseHeaders[name] = value;
            }

            public override void SendUnknownResponseHeader(string name, string value)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    ResponseHeaders[name] = value;
            }

            public override void SendResponseFromMemory(byte[] data, int length)
            {
                if (data == null || length <= 0) return;
                _responseBody.Write(data, 0, Math.Min(length, data.Length));
            }

            public override void SendResponseFromFile(string filename, long offset, long length)
            {
                // optional for your tests; implement if needed
                using (var fs = File.OpenRead(filename))
                {
                    if (offset > 0) fs.Seek(offset, SeekOrigin.Begin);

                    var remaining = length < 0 ? long.MaxValue : length;
                    var buffer = new byte[81920];

                    while (remaining > 0)
                    {
                        int read = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (read <= 0) break;
                        _responseBody.Write(buffer, 0, read);
                        remaining -= read;
                    }
                }
            }

            public override void SendResponseFromFile(IntPtr handle, long offset, long length) { }
            public override void FlushResponse(bool finalFlush) { }
            public override void EndOfRequest() => Ended = true;
        }

        public static class HttpTestFactory
        {
            public static (HttpContext context, TestHttpWorkerRequestWithResponse worker) CreateContext(Uri uri)
            {
                var worker = new TestHttpWorkerRequestWithResponse(uri);
                var ctx = new HttpContext(worker);
                return (ctx, worker);
            }
        }
    }
}
