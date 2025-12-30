using IISFrontGuard.Module.Models;
using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace IISFrontGuard.Module.Services
{
    internal static class RequestLogger
    {
        private static readonly ConcurrentQueue<SafeRequestData> _queue = new ConcurrentQueue<SafeRequestData>();
        private static readonly ConcurrentQueue<LogEntrySafeResponse> _responseQueue = new ConcurrentQueue<LogEntrySafeResponse>();
        private static string _connectionString = "";
        internal static event EventHandler<SafeRequestData> OnRequestLogged;
        internal static event EventHandler<LogEntrySafeResponse> OnResponseLogged;

        static RequestLogger()
        {
            Task.Factory.StartNew(ProcessQueueAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(ProcessResponseQueueAsync, TaskCreationOptions.LongRunning);
        }

        internal static void Enqueue(HttpRequest req, string connectionString, int? ruleTriggered, string rayId, string iso2, int? actionId, string appId)
        {
            _connectionString = connectionString;
            var safeRequestData = SafeRequestData.FromHttpRequest(req, ruleTriggered, rayId, iso2, actionId, appId, GetBody(req));
            _queue.Enqueue(safeRequestData);
        }

        internal static void EnqueueResponse(LogEntrySafeResponse logEntry, string connectionString, bool insertIntoDatabase = false)
        {
            _connectionString = connectionString;            
            if (insertIntoDatabase)
                Task.Run(async () => await InsertEntry(logEntry));
            else
                _responseQueue.Enqueue(logEntry);
        }

        private static volatile bool _isRunning = true;

        internal static void Stop()
            => _isRunning = false;

        private static async Task ProcessQueueAsync()
        {
            while (_isRunning)
            {
                while (_queue.TryDequeue(out var entry))
                {
                    try
                    {
                        await InsertEntry(entry);
                    }
                    catch
                    {
                        // Silently fail - logging errors shouldn't impact request processing
                    }
                }

                await Task.Delay(250); // Short pause to prevent CPU spinning
            }
        }

        private static async Task ProcessResponseQueueAsync()
        {
            while (_isRunning)
            {
                while (_responseQueue.TryDequeue(out LogEntrySafeResponse entry))
                {
                    try
                    {
                        await InsertEntry(entry);
                    }
                    catch
                    {
                        // Silently fail - logging errors shouldn't impact request processing
                    }
                }
                await Task.Delay(250); // Short pause to prevent CPU spinning
            }
        }

        private static async Task InsertEntry(SafeRequestData entry)
        {
            // Log to SQL Server
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand("InsertRequestContext", connection) { CommandType = System.Data.CommandType.StoredProcedure };

                command.Parameters.AddWithValue("@RayId", entry.RayId ?? "");
                command.Parameters.AddWithValue("@HostName", entry.HostName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@AppId", entry.AppId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@IPAddress", entry.IPAddress ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Protocol", entry.Protocol ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Referrer", entry.Referrer ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@HttpMethod", entry.HttpMethod ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@HttpVersion", entry.HttpVersion ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@UserAgent", entry.UserAgent ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@XForwardedFor", entry.XForwardedFor ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@MimeType", entry.MimeType ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@UrlFull", entry.UrlFull ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@UrlPath", entry.UrlPath ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@UrlPathAndQuery", entry.UrlPathAndQuery ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@UrlQueryString", entry.UrlQueryString ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@RuleId", entry.RuleId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ActionId", entry.ActionId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@RequestBody", entry.RequestBody ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CountryIso2", entry.CountryIso2 ?? (object)DBNull.Value);

                await command.ExecuteNonQueryAsync();
                OnRequestLogged?.Invoke(null, entry);
            }
        }

        private static async Task InsertEntry(LogEntrySafeResponse entry)
        {
            try
            {
                // Log response data to SQL Server
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    SqlCommand command = new SqlCommand("InsertResponseContext", connection) { CommandType = System.Data.CommandType.StoredProcedure };
                    command.Parameters.AddWithValue("@Url", entry.Url);
                    command.Parameters.AddWithValue("@HttpMethod", entry.HttpMethod);
                    command.Parameters.AddWithValue("@ResponseTime", entry.ResponseTime);
                    command.Parameters.AddWithValue("@Timestamp", entry.Timestamp);
                    command.Parameters.AddWithValue("@RayId", entry.RayId);
                    command.Parameters.AddWithValue("@StatusCode", entry.StatusCode ?? (object)DBNull.Value);
                    await command.ExecuteNonQueryAsync();

                    OnResponseLogged?.Invoke(null, entry);
                }
            }
            catch
            {
                // Silently fail - logging errors shouldn't impact request processing
            }
        }

        internal static string Encrypt(string clearText, string key)
        {
            string Result = "";
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] dataBytes = Encoding.UTF8.GetBytes(clearText);

            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                aes.GenerateIV(); 

                using (var encryptor = aes.CreateEncryptor())
                    Result = Convert.ToBase64String(aes.IV.Concat(encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length)).ToArray());
            }

            return Result;
        }

        internal static string Decrypt(string clearText, string key)
        {
            string Result = "";
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] encryptedBytesWithIV = Convert.FromBase64String(clearText);

            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                //Extract IV from the encrypted data
                aes.IV = encryptedBytesWithIV.Take(aes.BlockSize / 8).ToArray(); 
                byte[] encryptedBytes = encryptedBytesWithIV.Skip(aes.BlockSize / 8).ToArray();

                using (var decryptor = aes.CreateDecryptor())
                    Result = Encoding.UTF8.GetString(decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length));
            }
            return Result;
        }

        internal static int GetTokenExpirationDuration(string host, string connectionString)
        {
            //Query the database to fetch the token expiration duration
            using (var connection = new SqlConnection(string.IsNullOrWhiteSpace(_connectionString) ? connectionString : _connectionString))
            {
                connection.Open();
                var command = new SqlCommand($@"SELECT TokenExpirationDurationHr FROM AppEntity WHERE Host = @host", connection);
                command.Parameters.AddWithValue("@host", host);

                int.TryParse(command.ExecuteScalar()?.ToString(), out var duration);
                return duration > 0 ? duration : 12;
            }
        }

        internal static string GetBody(HttpRequest request)
        {
            try
            {
                const int MaxBodySize = 10 * 1024 * 1024; // 10MB limit

                if (request.ContentLength > MaxBodySize)
                    return string.Empty;

                // Reset position if already read
                if (request.InputStream.CanSeek)
                    request.InputStream.Position = 0;

                using (var reader = new System.IO.StreamReader(
                    request.InputStream,
                    request.ContentEncoding ?? Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 4096,
                    leaveOpen: true))
                {
                    var body = reader.ReadToEnd();

                    // Reset for subsequent reads
                    if (request.InputStream.CanSeek)
                        request.InputStream.Position = 0;

                    return body;
                }
            }
            catch
            {
                // In case of any error, return empty body
                return string.Empty;
            }
        }
    }
}