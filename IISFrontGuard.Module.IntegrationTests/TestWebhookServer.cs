using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IISFrontGuard.Module.IntegrationTests
{
    public sealed class TestWebhookServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _loop;

        private int _hitCount = 0;

        public int Port { get; }
        public string Url => $"http://localhost:{Port}/webhook/";

        public ConcurrentQueue<CapturedRequest> Requests { get; } = new ConcurrentQueue<CapturedRequest>();

        // Let tests control responses (e.g. fail 2 times then succeed)
        public Func<int, HttpListenerRequest, (int statusCode, string body)> ResponseFactory { get; set; }
            = (_, __) => (200, "ok");

        public TestWebhookServer(int port)
        {
            Port = port;
            _listener.Prefixes.Add(Url);
        }

        public void Start()
        {
            _listener.Start();
            _loop = Task.Run(ListenLoopAsync);
        }

        private async Task ListenLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                    var req = ctx.Request;

                    string body = "";
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                        body = await reader.ReadToEndAsync().ConfigureAwait(false);

                    var captured = new CapturedRequest(
                        method: req.HttpMethod,
                        rawUrl: req.RawUrl,
                        contentType: req.ContentType,
                        headers: req.Headers,
                        body: body
                    );

                    Requests.Enqueue(captured);

                    var attempt = Interlocked.Increment(ref _hitCount);
                    var (code, respBody) = ResponseFactory(attempt, req);

                    var buffer = Encoding.UTF8.GetBytes(respBody ?? "");
                    ctx.Response.StatusCode = code;
                    ctx.Response.ContentType = "text/plain";
                    ctx.Response.ContentLength64 = buffer.Length;
                    await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    ctx.Response.OutputStream.Close();
                }
                catch
                {
                    // swallow test server errors
                    try { ctx?.Response?.Abort(); } catch { /* no-op */ }
                }
            }
        }

        public async Task<CapturedRequest> WaitForRequestAsync(TimeSpan timeout)
        {
            var start = DateTime.UtcNow;
            while (DateTime.UtcNow - start < timeout)
            {
                if (Requests.TryDequeue(out var r))
                    return r;

                await Task.Delay(50).ConfigureAwait(false);
            }
            throw new TimeoutException("No webhook request received within timeout.");
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* no-op */ }
            try { _listener.Close(); } catch { /* no-op */ }
            if (_loop != null)
            {
                try { _loop.ConfigureAwait(false); } catch { /* no-op */ }
            }
            _cts.Dispose();
        }
    }

    public sealed class CapturedRequest
    {
        public string Method { get; }
        public string RawUrl { get; }
        public string ContentType { get; }
        public NameValueCollection Headers { get; }
        public string Body { get; }

        public CapturedRequest(string method, string rawUrl, string contentType, NameValueCollection headers, string body)
        {
            Method = method;
            RawUrl = rawUrl;
            ContentType = contentType;
            Headers = headers;
            Body = body;
        }
    }
}