using IISFrontGuard.Module.Abstractions;
using System.Web;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Provides access to the current HttpContext for the executing request.
    /// </summary>
    public class HttpContextAccessor : IHttpContextAccessor
    {
        /// <summary>
        /// Gets the current HttpContext for the executing HTTP request.
        /// </summary>
        public HttpContext Current => HttpContext.Current;

        /// <summary>
        /// Sets an item in the current HttpContext.Items collection.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <param name="value">The item value.</param>
        public void SetContextItem(string key, object value)
        {
            if (HttpContext.Current != null)
                HttpContext.Current.Items[key] = value;
        }

        /// <summary>
        /// Retrieves an item from the current HttpContext.Items collection.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <returns>The item value, or null if not found.</returns>
        public object GetContextItem(string key)
            => HttpContext.Current?.Items[key];

        /// <summary>
        /// Completes the current request, preventing further processing.
        /// </summary>
        public void CompleteRequest()
            => HttpContext.Current?.ApplicationInstance?.CompleteRequest();
    }
}