using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.SqlClient;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.FrontGuard
{
    [Collection("IIS Integration Tests")]
    public class GenerateAndSetTokenEndToEndTests
    {
        private readonly IisIntegrationFixture _fixture;

        public GenerateAndSetTokenEndToEndTests(IisIntegrationFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [Fact]
        public async Task ManagedChallenge_PostReturnsRedirectAndSetsCookie()
        {
            // Ensure encryption key is set in web.config for the running IIS site
            var encryptionKey = "1234567890123456"; // 16 bytes for AES
            _fixture.UpdateWebConfigAppSetting("IISFrontGuardEncryptionKey", encryptionKey);
            await _fixture.RecycleAppPoolAsync();

            // 1) Trigger managed challenge page (seeded rule matches path containing 'managed')
            var getResp = await _fixture.Client.GetAsync("/managed");
            Assert.Equal(HttpStatusCode.Forbidden, getResp.StatusCode);

            var body = await getResp.Content.ReadAsStringAsync();

            // Extract rayId and csrf token from the returned HTML using a simple parser
            string ExtractInputValue(string html, string inputName)
            {
                var marker = $"name=\"{inputName}\" value=\"";
                var idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return null;
                idx += marker.Length;
                var end = html.IndexOf('"', idx);
                if (end < 0) return null;
                return html.Substring(idx, end - idx);
            }

            var rayId = ExtractInputValue(body, "__rayId");
            var csrf = ExtractInputValue(body, "__csrf");

            Assert.False(string.IsNullOrEmpty(rayId), "__rayId not found in challenge page");
            Assert.False(string.IsNullOrEmpty(csrf), "__csrf not found in challenge page");

            // Integration assertions: CSP should be present on HTML responses
            Assert.True(getResp.Headers.Contains("Content-Security-Policy") || getResp.Content.Headers.Contains("Content-Security-Policy"), "CSP header missing on challenge GET response");

            // If the site is served over HTTPS, HSTS should be present
            if (_fixture.BaseUri != null && string.Equals(_fixture.BaseUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(getResp.Headers.Contains("Strict-Transport-Security") || getResp.Content.Headers.Contains("Strict-Transport-Security"), "HSTS header missing on HTTPS challenge GET response");
            }

            // 2) Post back the form to receive the clearance token cookie
            var postContent = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("__rayId", rayId),
                new KeyValuePair<string, string>("__csrf", csrf)
            });

            var postResp = await _fixture.Client.PostAsync("/managed", postContent);

            // GenerateAndSetToken performs a Redirect; HttpClient configured with AllowAutoRedirect = false
            Assert.True(postResp.StatusCode == HttpStatusCode.Found || 
                postResp.StatusCode == HttpStatusCode.OK);

            // Cookie should be set on the response headers
            Assert.True(postResp.Headers.Contains("Set-Cookie"), "Set-Cookie header missing on response");
            var setCookies = postResp.Headers.GetValues("Set-Cookie");
            bool found = false;
            foreach (var c in setCookies)
            {
                if (c.StartsWith("fgm_clearance=")) { found = true; break; }
            }
            Assert.True(found, "fgm_clearance cookie not set by GenerateAndSetToken");
        }
    }
}
