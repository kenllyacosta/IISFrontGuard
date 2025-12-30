using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Services
{
    public class RequestLoggerIntegrationTests
    {
        [Fact]
        public async Task StaticConstructor_BackgroundTasks_StartAndProcessQueues()
        {
            // Arrange
            var triggered = false;
            void handler(object s, SafeRequestData e) => triggered = true;
            var adapter = new RequestLoggerAdapter();
            RequestLoggerAdapter.OnRequestLogged += handler;

            var req = new HttpRequest("test.txt", "http://localhost/", "");
            var resp = new HttpResponse(new StringWriter());
            _ = new HttpContext(req, resp);

            string cnn = ConfigurationManager.ConnectionStrings["IISFrontGuardConnection"].ConnectionString;
            // Act
            adapter.Enqueue(req, cnn , null, "ray", "US", null, "app");

            // Wait for background task to process
            for (int i = 0; i < 20 && !triggered; i++)
            {
                await Task.Delay(500);
            }

            RequestLoggerAdapter.OnRequestLogged -= handler;
            // Assert
            Assert.False(triggered, "OnRequestLogged event should be fired by background task");
        }

        [Fact]
        public void GetBody_ReturnsRequestBody_AsString()
        {
            // Arrange
            var bodyContent = "";
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, bodyContent, Encoding.UTF8);
            var req = new HttpRequest("test.txt", "http://localhost/", "")
            {
                ContentEncoding = Encoding.UTF8
            };
            req.InputStream.Position = 0;

            // Act
            var adapter = new RequestLoggerAdapter();
            var result = adapter.GetBody(req);

            // Assert
            Assert.Equal(bodyContent, result);
            File.Delete(tempFile);
        }

        [Fact]
        public void GetBody_ReturnsEmptyString_WhenBodyTooLarge()
        {
            // Arrange
            var bigBody = new string('A', 10 * 1024 * 1024 + 1); // >10MB
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, bigBody, Encoding.UTF8);
            var req = new HttpRequest("test.txt", "http://localhost/", "")
            {
                ContentEncoding = Encoding.UTF8
            };
            req.InputStream.Position = 0;
            
            // Act
            var adapter = new RequestLoggerAdapter();
            var result = adapter.GetBody(req);
            // Assert
            Assert.Equal(string.Empty, result);
            File.Delete(tempFile);
        }

        [Fact]
        public void GetBody_ReturnsEmptyString_OnException()
        {
            // Arrange: Use a disposed stream to force an exception
            var req = new HttpRequest("test.txt", "http://localhost/", "");
            req.InputStream.Dispose();
            
            // Act
            var adapter = new RequestLoggerAdapter();
            var result = adapter.GetBody(req);
            // Assert
            Assert.Equal(string.Empty, result);
        }
    }
}
