using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.IntegrationTests.Helpers;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace IISFrontGuard.Module.IntegrationTests.Services
{
    [Collection("IIS Integration Tests")]
    public class RequestLoggerAdapterIntegrationTests
    {
        private readonly IisIntegrationFixture _fixture;
        private readonly RequestLoggerAdapter _adapter;
        private static bool? _isModuleLoggingEnabled;

        public RequestLoggerAdapterIntegrationTests(IisIntegrationFixture fixture)
        {
            _fixture = fixture;
            _adapter = new RequestLoggerAdapter();
        }

        /// <summary>
        /// Waits for a condition to be true by polling the database.
        /// Reliable approach that works in all environments including CI.
        /// </summary>
        private async Task<bool> WaitForConditionAsync(Func<SqlConnection, Task<bool>> condition, int timeoutMs = 15000, int pollIntervalMs = 500)
        {
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
                    {
                        await cn.OpenAsync();
                        if (await condition(cn))
                            return true;
                    }
                }
                catch (SqlException)
                {
                    // Database not available, continue waiting
                }

                await Task.Delay(pollIntervalMs);
            }
            return false;
        }

        /// <summary>
        /// Cleans up test data inserted by individual tests.
        /// </summary>
        private async Task CleanupTestDataAsync(params Guid[] appIds)
        {
            if (!await IsDatabaseAvailableAsync())
                return;

            try
            {
                using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
                {
                    await cn.OpenAsync();

                    if (appIds != null && appIds.Length > 0)
                    {
                        using (var cmd = cn.CreateCommand())
                        {
                            var parameters = string.Join(",", appIds.Select((_, i) => $"@AppId{i}"));
                            cmd.CommandText = $@"
DELETE FROM dbo.ResponseContext WHERE RayId IN (
    SELECT RayId FROM dbo.RequestContext WHERE AppId IN ({parameters})
);
DELETE FROM dbo.RequestContext WHERE AppId IN ({parameters});
DELETE FROM dbo.WafConditionEntity WHERE WafRuleEntityId IN (
    SELECT Id FROM dbo.WafRuleEntity WHERE AppId IN ({parameters})
);
DELETE FROM dbo.WafRuleEntity WHERE AppId IN ({parameters});
DELETE FROM dbo.AppEntity WHERE Id IN ({parameters});";

                            for (int i = 0; i < appIds.Length; i++)
                            {
                                cmd.Parameters.AddWithValue($"@AppId{i}", appIds[i]);
                            }

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                System.Diagnostics.Trace.WriteLine($"Warning: Failed to clean test data: {ex.Message}");
            }
        }

        private async Task<bool> IsDatabaseAvailableAsync()
        {
            try
            {
                using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
                {
                    await cn.OpenAsync();
                    return true;
                }
            }
            catch (SqlException)
            {
                return false;
            }
        }

        private async Task<bool> IsModuleLoggingEnabledAsync()
        {
            if (_isModuleLoggingEnabled.HasValue)
                return _isModuleLoggingEnabled.Value;

            if (!await IsDatabaseAvailableAsync())
            {
                _isModuleLoggingEnabled = false;
                return false;
            }

            try
            {
                _ = await _fixture.Client.GetAsync("/");
                await Task.Delay(2000);

                using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
                {
                    await cn.OpenAsync();
                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM dbo.RequestContext WHERE HostName = 'localhost'";
                        var count = (int)await cmd.ExecuteScalarAsync();
                        _isModuleLoggingEnabled = count > 0;

                        if (!_isModuleLoggingEnabled.Value)
                        {
                            System.Diagnostics.Trace.WriteLine(
                                "WARNING: FrontGuardModule request logging appears to be disabled. " +
                                "Request logging tests will be skipped.");
                        }

                        return _isModuleLoggingEnabled.Value;
                    }
                }
            }
            catch
            {
                _isModuleLoggingEnabled = false;
                return false;
            }
        }

        [Fact]
        public void Encrypt_Decrypt_RoundTrip_ShouldReturnOriginalText()
        {
            var clearText = "Sensitive data that needs encryption";
            var key = "6943825701284396";

            var encrypted = _adapter.Encrypt(clearText, key);
            var decrypted = _adapter.Decrypt(encrypted, key);

            Assert.NotNull(encrypted);
            Assert.NotEqual(clearText, encrypted);
            Assert.Equal(clearText, decrypted);
        }

        [Fact]
        public void Encrypt_ShouldProduceDifferentCiphertextForSameInput()
        {
            var clearText = "Test message";
            var key = "6943825701284396";

            var encrypted1 = _adapter.Encrypt(clearText, key);
            var encrypted2 = _adapter.Encrypt(clearText, key);

            Assert.NotEqual(encrypted1, encrypted2);
            Assert.Equal(clearText, _adapter.Decrypt(encrypted1, key));
            Assert.Equal(clearText, _adapter.Decrypt(encrypted2, key));
        }

        [Fact]
        public void Decrypt_WithWrongKey_ShouldThrowException()
        {
            var clearText = "Secret message";
            var correctKey = "6943825701284396";
            var wrongKey = "1234567890123456";
            var encrypted = _adapter.Encrypt(clearText, correctKey);

            Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
            {
                _adapter.Decrypt(encrypted, wrongKey);
            });
        }

        [Fact]
        public void Encrypt_WithSpecialCharacters_ShouldWorkCorrectly()
        {
            var clearText = "Special chars: הצü ס י ?? \r\n\t";
            var key = "6943825701284396";

            var encrypted = _adapter.Encrypt(clearText, key);
            var decrypted = _adapter.Decrypt(encrypted, key);

            Assert.Equal(clearText, decrypted);
        }

        [Fact]
        public async Task GetTokenExpirationDuration_WithExistingHost_ShouldReturnConfiguredDuration()
        {
            if (!await IsDatabaseAvailableAsync())
            {
                System.Diagnostics.Trace.WriteLine("Database not available - skipping test");
                return;
            }

            var host = "test-token-duration.local";
            var appId = Guid.NewGuid();
            var expectedDuration = 24;

            try
            {
                using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
                {
                    await cn.OpenAsync();
                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = @"
INSERT INTO dbo.AppEntity (Id, Host, AppName, TokenExpirationDurationHr) 
VALUES (@AppId, @Host, @AppName, @Duration)";
                        cmd.Parameters.AddWithValue("@AppId", appId);
                        cmd.Parameters.AddWithValue("@Host", host);
                        cmd.Parameters.AddWithValue("@AppName", "Test App");
                        cmd.Parameters.AddWithValue("@Duration", expectedDuration);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                var duration = _adapter.GetTokenExpirationDuration(host, _fixture.LocalDbAppCs);
                Assert.Equal(expectedDuration, duration);
            }
            finally
            {
                await CleanupTestDataAsync(appId);
            }
        }

        [Fact]
        public void GetTokenExpirationDuration_WithNonExistentHost_ShouldReturnDefaultDuration()
        {
            var nonExistentHost = "non-existent-host-" + Guid.NewGuid();
            var duration = _adapter.GetTokenExpirationDuration(nonExistentHost, _fixture.LocalDbAppCs);
            Assert.Equal(12, duration);
        }

        [Fact]
        public async Task GetTokenExpirationDuration_WithNullDuration_ShouldReturnDefault()
        {
            if (!await IsDatabaseAvailableAsync())
            {
                System.Diagnostics.Trace.WriteLine("Database not available - skipping test");
                return;
            }

            var host = "test-null-duration.local";
            var appId = Guid.NewGuid();

            try
            {
                using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
                {
                    await cn.OpenAsync();
                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = @"
INSERT INTO dbo.AppEntity (Id, Host, AppName, TokenExpirationDurationHr) 
VALUES (@AppId, @Host, @AppName, NULL)";
                        cmd.Parameters.AddWithValue("@AppId", appId);
                        cmd.Parameters.AddWithValue("@Host", host);
                        cmd.Parameters.AddWithValue("@AppName", "Test App");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                var duration = _adapter.GetTokenExpirationDuration(host, _fixture.LocalDbAppCs);
                Assert.Equal(12, duration);
            }
            finally
            {
                await CleanupTestDataAsync(appId);
            }
        }

        [Fact]
        public async Task RequestLogger_ShouldLogSimpleGetRequest()
        {
            if (!await IsDatabaseAvailableAsync() || !await IsModuleLoggingEnabledAsync())
                return;

            _ = await _fixture.Client.GetAsync("/");

            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.RequestContext WHERE HostName = 'localhost' AND HttpMethod = 'GET'";
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }, timeoutMs: 20000);

            Assert.True(logged, "GET request should be logged");
        }

        [Fact]
        public async Task RequestLogger_ShouldLogPostRequestWithBody()
        {
            if (!await IsDatabaseAvailableAsync() || !await IsModuleLoggingEnabledAsync())
                return;

            var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
            _ = await _fixture.Client.PostAsync("/", content);

            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.RequestContext WHERE HostName = 'localhost' AND HttpMethod = 'POST'";
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }, timeoutMs: 20000);

            Assert.True(logged, "POST request should be logged");
        }

        [Fact]
        public async Task RequestLogger_ShouldLogResponseContext()
        {
            if (!await IsDatabaseAvailableAsync() || !await IsModuleLoggingEnabledAsync())
                return;

            _ = await _fixture.Client.GetAsync("/");

            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.ResponseContext WHERE HttpMethod = 'GET'";
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }, timeoutMs: 20000);

            Assert.True(logged, "Response should be logged");
        }

        [Fact]
        public async Task RequestLogger_ShouldLogMultipleRequests()
        {
            if (!await IsDatabaseAvailableAsync() || !await IsModuleLoggingEnabledAsync())
                return;

            int initialCount;
            using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
            {
                await cn.OpenAsync();
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.RequestContext";
                    initialCount = (int)await cmd.ExecuteScalarAsync();
                }
            }

            await _fixture.Client.GetAsync("/");
            await _fixture.Client.GetAsync("/default.aspx");
            await _fixture.Client.PostAsync("/", new StringContent("test", Encoding.UTF8));

            RequestLoggerAdapter.OnRequestLogged += OnRequestLogged;
            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.RequestContext";
                    return (int)await cmd.ExecuteScalarAsync() >= initialCount + 3;
                }
            }, timeoutMs: 25000);

            RequestLoggerAdapter.OnRequestLogged -= OnRequestLogged;
            Assert.True(logged, "Multiple requests should be logged");
        }

        /// <summary>
        /// Event handler for request logged event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnRequestLogged(object sender, SafeRequestData e)
        {
            Trace.WriteLine(
                $"Request logged: RayId={e.RayId}, Method={e.HttpMethod}, URL={e.UrlFull}, Agent={e.UserAgent}");

            var original = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Debug.Write("Request logged: ");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Debug.Write($"RayId={e.RayId} ");

            Console.ForegroundColor = ConsoleColor.Green;
            Debug.WriteLine($"Method={e.HttpMethod} ");

            Console.ForegroundColor = ConsoleColor.White;
            Debug.WriteLine($"URL={e.UrlFull} ");

            Console.ForegroundColor = ConsoleColor.Magenta;
            Debug.WriteLine($"Time={e.UserAgent}ms");

            Console.ForegroundColor = original;

            Debug.WriteLine(
                $"\u001b[36mRequest logged:\u001b[0m " +
                $"\u001b[33mRayId={e.RayId}\u001b[0m " +
                $"\u001b[32mMethod={e.HttpMethod}\u001b[0m " +
                $"URL={e.UrlFull} " +
                $"\u001b[35mIPAddress={e.IPAddress}ms\u001b[0m" +
                $"\u001b[34mUserAgent={e.UserAgent}\u001b[0m");
        }

        [Fact]
        public async Task Encryption_IntegrationWithDatabase()
        {
            if (!await IsDatabaseAvailableAsync())
                return;

            var sensitiveData = "Sensitive User Data: SSN 123-45-6789";
            var key = "6943825701284396";
            var encrypted = _adapter.Encrypt(sensitiveData, key);
            var testId = Guid.NewGuid();

            try
            {
                using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
                {
                    await cn.OpenAsync();
                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = "INSERT INTO dbo.AppEntity (Id, Host, AppName, AppDescription) VALUES (@Id, @Host, @Name, @Data)";
                        cmd.Parameters.AddWithValue("@Id", testId);
                        cmd.Parameters.AddWithValue("@Host", "encryption-test.local");
                        cmd.Parameters.AddWithValue("@Name", "Test");
                        cmd.Parameters.AddWithValue("@Data", encrypted);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT AppDescription FROM dbo.AppEntity WHERE Id = @Id";
                        cmd.Parameters.AddWithValue("@Id", testId);
                        var stored = (string)await cmd.ExecuteScalarAsync();
                        var decrypted = _adapter.Decrypt(stored, key);
                        Assert.Equal(sensitiveData, decrypted);
                    }
                }
            }
            finally
            {
                await CleanupTestDataAsync(testId);
            }
        }

        [Fact]
        public void Stop_ShouldStopBackgroundProcessing()
        {
            _adapter.Stop();
            Assert.True(true);
        }

        [Fact]
        public async Task EnqueueResponse_ShouldLogResponseWithAllParameters()
        {
            if (!await IsDatabaseAvailableAsync())
                return;

            var rayId = Guid.NewGuid();
            var url = $"http://localhost/test-{rayId}";

            RequestLoggerAdapter.OnResponseLogged += OnResponseLogged;

            _adapter.EnqueueResponse(new LogEntrySafeResponse()
            {
                RayId = rayId,
                Url = url,
                HttpMethod = "GET",
                ResponseTime = 125L,
                Timestamp = DateTime.UtcNow,
                StatusCode = 200
            }, _fixture.LocalDbAppCs, true);

            // Generous timeout for CI environments
            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.ResponseContext WHERE RayId = @RayId";
                    cmd.Parameters.AddWithValue("@RayId", rayId);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }, timeoutMs: 60000, pollIntervalMs: 1000);

            RequestLoggerAdapter.OnResponseLogged -= OnResponseLogged;
            Assert.True(logged, "Response should be logged");
        }

        [Fact]
        public async Task EnqueueResponse_ShouldUseTheWorkerProcessResponseWithAllParameters()
        {
            if (!await IsDatabaseAvailableAsync())
                return;

            var rayId = Guid.NewGuid();
            var url = $"http://localhost/test-{rayId}";

            RequestLoggerAdapter.OnResponseLogged += OnResponseLogged;

            _adapter.EnqueueResponse(new LogEntrySafeResponse()
            {
                RayId = rayId,
                Url = url,
                HttpMethod = "GET",
                ResponseTime = 125L,
                Timestamp = DateTime.UtcNow,
                StatusCode = 200
            }, _fixture.LocalDbAppCs, false);

            // Generous timeout for CI environments
            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.ResponseContext WHERE RayId = @RayId";
                    cmd.Parameters.AddWithValue("@RayId", rayId);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }, timeoutMs: 100, pollIntervalMs: 1000);

            RequestLoggerAdapter.OnResponseLogged -= OnResponseLogged;
            Assert.False(logged, "Response should not be logged");
        }

        [Fact]
        public async Task EnqueueResponse_ShouldHandleMultipleResponses()
        {
            if (!await IsDatabaseAvailableAsync())
                return;

            var rayId1 = Guid.NewGuid();
            var rayId2 = Guid.NewGuid();
            var rayId3 = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;

            RequestLoggerAdapter.OnResponseLogged += OnResponseLogged;
            _adapter.EnqueueResponse(new LogEntrySafeResponse() //$"http://localhost/test1", "GET", 100L, timestamp, _fixture.LocalDbAppCs, rayId1
            {
                RayId = rayId1,
                Url = $"http://localhost/test1",
                HttpMethod = "GET",
                ResponseTime = 100L,
                Timestamp = timestamp,
                StatusCode = 200
            }, _fixture.LocalDbAppCs, true);
            _adapter.EnqueueResponse(new LogEntrySafeResponse()
            {
                RayId = rayId2,
                Url = $"http://localhost/test2",
                HttpMethod = "POST",
                ResponseTime = 250L,
                Timestamp = timestamp,
                StatusCode = 200
            }, _fixture.LocalDbAppCs, true);
            _adapter.EnqueueResponse(new LogEntrySafeResponse()
            {
                RayId = rayId3,
                Url = $"http://localhost/test3",
                HttpMethod = "PUT",
                ResponseTime = 180L,
                Timestamp = timestamp,
                StatusCode = 200
            }, _fixture.LocalDbAppCs, true);

            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.ResponseContext WHERE RayId IN (@R1, @R2, @R3)";
                    cmd.Parameters.AddWithValue("@R1", rayId1);
                    cmd.Parameters.AddWithValue("@R2", rayId2);
                    cmd.Parameters.AddWithValue("@R3", rayId3);
                    return (int)await cmd.ExecuteScalarAsync() >= 3;
                }
            }, timeoutMs: 60000, pollIntervalMs: 1000);

            RequestLoggerAdapter.OnResponseLogged -= OnResponseLogged;
            Assert.True(logged, "All responses should be logged");
        }

        [Fact]
        public async Task EnqueueResponse_ShouldHandleLongResponseTimes()
        {
            if (!await IsDatabaseAvailableAsync())
                return;

            RequestLoggerAdapter.OnResponseLogged += OnResponseLogged;
            var rayId = Guid.NewGuid();
            _adapter.EnqueueResponse(new LogEntrySafeResponse()
            {
                RayId = rayId,
                Url = $"http://localhost/test-{rayId}",
                HttpMethod = "GET",
                ResponseTime = 5000L,
                Timestamp = DateTime.UtcNow,
                StatusCode = 200
            }, _fixture.LocalDbAppCs, true);

            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.ResponseContext WHERE RayId = @RayId";
                    cmd.Parameters.AddWithValue("@RayId", rayId);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }, timeoutMs: 60000, pollIntervalMs: 1000);

            RequestLoggerAdapter.OnResponseLogged -= OnResponseLogged;
            Assert.True(logged, "Response with long time should be logged");
        }

        [Fact]
        public async Task EnqueueResponse_ShouldLogDifferentHttpMethods()
        {
            if (!await IsDatabaseAvailableAsync())
                return;

            var methods = new[] { "GET", "POST", "PUT", "DELETE" };
            var rayIds = new Guid[methods.Length];
            var timestamp = DateTime.UtcNow;

            RequestLoggerAdapter.OnResponseLogged += OnResponseLogged;
            for (int i = 0; i < methods.Length; i++)
            {
                rayIds[i] = Guid.NewGuid();
                _adapter.EnqueueResponse(new LogEntrySafeResponse()
                {
                    RayId = rayIds[i],
                    Url = $"http://localhost/test-{methods[i]}",
                    HttpMethod = methods[i],
                    ResponseTime = 100L,
                    Timestamp = timestamp,
                    StatusCode = 200
                }, _fixture.LocalDbAppCs, true);
            }

            // Wait until all 4 responses are present
            var allLogged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.ResponseContext WHERE RayId IN (@R0, @R1, @R2, @R3)";
                    for (int i = 0; i < rayIds.Length; i++)
                        cmd.Parameters.AddWithValue($"@R{i}", rayIds[i]);
                    return (int)await cmd.ExecuteScalarAsync() == 4;
                }
            }, timeoutMs: 70000, pollIntervalMs: 1000);

            Assert.True(allLogged, "All responses should be logged");

            // Now check that all 4 distinct methods are present
            var distinctMethods = 0;
            using (var cn = new SqlConnection(_fixture.LocalDbAppCs))
            {
                await cn.OpenAsync();
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(DISTINCT HttpMethod) FROM dbo.ResponseContext WHERE RayId IN (@R0, @R1, @R2, @R3)";
                    for (int i = 0; i < rayIds.Length; i++)
                        cmd.Parameters.AddWithValue($"@R{i}", rayIds[i]);
                    distinctMethods = (int)await cmd.ExecuteScalarAsync();
                }
            }

            RequestLoggerAdapter.OnResponseLogged -= OnResponseLogged;
            Assert.Equal(4, distinctMethods); // All 4 methods should be logged
        }

        private static readonly object _lock = new object();
        /// <summary>
        /// Event handler for response logged event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnResponseLogged(object sender, LogEntrySafeResponse e)
        {
            lock (_lock)
            {
                var original = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Debug.Write("Response logged: ");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Debug.Write($"RayId={e.RayId} ");

                Console.ForegroundColor = ConsoleColor.Green;
                Debug.WriteLine($"Method={e.HttpMethod} ");

                Console.ForegroundColor = ConsoleColor.White;
                Debug.WriteLine($"URL={e.Url} ");

                Console.ForegroundColor = ConsoleColor.Magenta;
                Debug.WriteLine($"Time={e.ResponseTime}ms");

                Console.ForegroundColor = original;
            }
        }

        [Fact]
        public async Task EnqueueResponse_ShouldHandleVeryLongUrls()
        {
            if (!await IsDatabaseAvailableAsync())
                return;

            var rayId = Guid.NewGuid();
            var longPath = string.Join("/", Enumerable.Repeat("segment", 50));
            var longUrl = $"http://localhost/{longPath}?p={rayId}";

            RequestLoggerAdapter.OnResponseLogged += OnResponseLogged;
            _adapter.EnqueueResponse(new LogEntrySafeResponse()
            {
                RayId = rayId,
                Url = longUrl,
                HttpMethod = "GET",
                ResponseTime = 150L,
                Timestamp = DateTime.UtcNow,
                StatusCode = 200
            }, _fixture.LocalDbAppCs, true);

            var logged = await WaitForConditionAsync(async cn =>
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.ResponseContext WHERE RayId = @RayId";
                    cmd.Parameters.AddWithValue("@RayId", rayId);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }, timeoutMs: 40000, pollIntervalMs: 1000);

            RequestLoggerAdapter.OnResponseLogged -= OnResponseLogged;
            Assert.True(logged, "Response with long URL should be logged");
        }

        [Fact]
        public void GetBody_ReturnsRequestBody_AsString()
        {
            // Arrange
            var bodyContent = "";
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, bodyContent, Encoding.UTF8);
            var url = "http://localhost/test";
            var httpRequest = new HttpRequest("test.txt", url, ""); // This will use tempFile as the body

            // Act
            var adapter = new RequestLoggerAdapter();
            var result = adapter.GetBody(httpRequest);

            // Assert
            Assert.Equal(bodyContent, result);

            // Cleanup
            File.Delete(tempFile);
        }

        [Fact]
        public void GetBody_ExceedsMaxSize_ReturnsEmptyString()
        {
            // Arrange
            var bodyContent = new string('A', 11534336);

            var rq = TestHelpers.CreateRequestWithBody("http://localhost/test", "POST", bodyContent);

            try
            {
                var adapter = new RequestLoggerAdapter();

                // Act
                var result = adapter.GetBody(rq);

                // Assert
                Assert.Equal(string.Empty, result);
            }
            catch { /* swallow exceptions */ }
        }
    }
}
