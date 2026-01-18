using IISFrontGuard.Module.Models;
using MaxMind.GeoIP2.Responses;
using System;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Helpers
{
    /// <summary>
    /// Test helper class for creating test instances of model objects.
    /// </summary>
    public static class TestModelFactory
    {
        /// <summary>
        /// Creates a RequestContext for testing with settable properties.
        /// Since RequestContext requires an HttpRequest and we can't easily mock it in unit tests,
        /// we use reflection to set the private fields and bypass the constructor validation.
        /// </summary>
        public static RequestContext CreateRequestContext(
            string method = "GET",
            string path = "/",
            string userAgent = "TestAgent",
            string clientIp = "127.0.0.1",
            string host = "test.local",
            string protocol = "https",
            string queryString = "",
            string referrer = "",
            string contentType = "",
            string countryIso2 = "",
            string countryName = "",
            string continentName = "",
            long bodyLength = 0)
        {
            // Create a minimal RequestContext using reflection
            // This avoids the HttpRequest requirement for pure unit tests
            var context = (RequestContext)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(RequestContext));

            // Set properties directly
            typeof(RequestContext).GetProperty(nameof(RequestContext.Method))?.SetValue(context, method);
            typeof(RequestContext).GetProperty(nameof(RequestContext.Path))?.SetValue(context, path);
            typeof(RequestContext).GetProperty(nameof(RequestContext.PathAndQuery))?.SetValue(context, path + queryString);
            typeof(RequestContext).GetProperty(nameof(RequestContext.UserAgent))?.SetValue(context, userAgent);
            typeof(RequestContext).GetProperty(nameof(RequestContext.ClientIp))?.SetValue(context, clientIp);
            typeof(RequestContext).GetProperty(nameof(RequestContext.Host))?.SetValue(context, host);
            typeof(RequestContext).GetProperty(nameof(RequestContext.Protocol))?.SetValue(context, protocol);
            typeof(RequestContext).GetProperty(nameof(RequestContext.QueryString))?.SetValue(context, queryString);
            typeof(RequestContext).GetProperty(nameof(RequestContext.FullUrl))?.SetValue(context, $"{protocol}://{host}{path}{queryString}");
            typeof(RequestContext).GetProperty(nameof(RequestContext.Referrer))?.SetValue(context, referrer);
            typeof(RequestContext).GetProperty(nameof(RequestContext.ContentType))?.SetValue(context, contentType);
            typeof(RequestContext).GetProperty(nameof(RequestContext.CountryIso2))?.SetValue(context, countryIso2);
            typeof(RequestContext).GetProperty(nameof(RequestContext.CountryName))?.SetValue(context, countryName);
            typeof(RequestContext).GetProperty(nameof(RequestContext.ContinentName))?.SetValue(context, continentName);
            typeof(RequestContext).GetProperty(nameof(RequestContext.HttpVersion))?.SetValue(context, "HTTP/1.1");
            typeof(RequestContext).GetProperty(nameof(RequestContext.XForwardedFor))?.SetValue(context, "");
            typeof(RequestContext).GetProperty(nameof(RequestContext.MimeType))?.SetValue(context, "");
            typeof(RequestContext).GetProperty(nameof(RequestContext.BodyLength))?.SetValue(context, bodyLength);

            return context;
        }
    }
}
