using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using System;
using System.Web;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Adapter that wraps the static RequestLogger class to provide instance-based dependency injection.
    /// </summary>
    public class RequestLoggerAdapter : IRequestLogger
    {
        /// <summary>
        /// Event raised when a request is logged to the database.
        /// </summary>
        public static event EventHandler<SafeRequestData> OnRequestLogged
        {
            add { RequestLogger.OnRequestLogged += value; }
            remove { RequestLogger.OnRequestLogged -= value; }
        }

        /// <summary>
        /// Event raised when a response is logged to the database.
        /// </summary>
        public static event EventHandler<LogEntrySafeResponse> OnResponseLogged
        {
            add { RequestLogger.OnResponseLogged += value; }
            remove { RequestLogger.OnResponseLogged -= value; }
        }

        /// <summary>
        /// Enqueues an HTTP request for asynchronous logging.
        /// </summary>
        /// <param name="req">The HTTP request to log.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="ruleTriggered">The WAF rule that was triggered (if any).</param>
        /// <param name="rayId">The unique Ray ID for this request.</param>
        /// <param name="iso2">The two-letter ISO country code.</param>
        /// <param name="actionId">The action that was taken (if any).</param>
        /// <param name="appId">The application identifier.</param>
        public void Enqueue(HttpRequest req, string connectionString, int? ruleTriggered, string rayId, string iso2, int? actionId, string appId)
        {
            RequestLogger.Enqueue(req, connectionString, ruleTriggered, rayId, iso2, actionId, appId);
        }

        /// <summary>
        /// Enqueues an HTTP response for asynchronous logging.
        /// </summary>
        /// <param name="logEntry">The log entry containing response details.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="insertIntoDatabase">Whether to insert the log entry into the database.</param>
        public void EnqueueResponse(LogEntrySafeResponse logEntry, string connectionString, bool insertIntoDatabase = false)
        {
            RequestLogger.EnqueueResponse(logEntry, connectionString, insertIntoDatabase);
        }

        /// <summary>
        /// Encrypts a string using AES encryption.
        /// </summary>
        /// <param name="clearText">The plaintext to encrypt.</param>
        /// <param name="key">The encryption key.</param>
        /// <returns>The encrypted string.</returns>
        public string Encrypt(string clearText, string key)
        {
            return RequestLogger.Encrypt(clearText, key);
        }

        /// <summary>
        /// Decrypts an AES-encrypted string.
        /// </summary>
        /// <param name="cipherText">The encrypted text to decrypt.</param>
        /// <param name="key">The decryption key.</param>
        /// <returns>The decrypted plaintext.</returns>
        public string Decrypt(string cipherText, string key)
        {
            return RequestLogger.Decrypt(cipherText, key);
        }

        /// <summary>
        /// Gets the token expiration duration in hours for a specific host.
        /// </summary>
        /// <param name="host">The hostname.</param>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>The token expiration duration in hours.</returns>
        public int GetTokenExpirationDuration(string host, string connectionString)
        {
            return RequestLogger.GetTokenExpirationDuration(host, connectionString);
        }

        /// <summary>
        /// Reads and returns the request body.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The request body as a string.</returns>
        public string GetBody(HttpRequest request)
        {
            return RequestLogger.GetBody(request);
        }

        /// <summary>
        /// Stops the background logging service gracefully.
        /// </summary>
        public void Stop()
        {
            RequestLogger.Stop();
        }
    }
}
