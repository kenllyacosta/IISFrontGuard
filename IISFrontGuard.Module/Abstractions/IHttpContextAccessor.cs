using System.Web;

namespace IISFrontGuard.Module.Abstractions
{
    /// <summary>
    /// Defines an abstraction for accessing HttpContext to enable testability.
    /// </summary>
    public interface IHttpContextAccessor
    {
        /// <summary>
        /// Gets the current HttpContext.
        /// </summary>
        HttpContext Current { get; }

        /// <summary>
        /// Sets an item in the current HttpContext.Items collection.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <param name="value">The item value.</param>
        void SetContextItem(string key, object value);

        /// <summary>
        /// Retrieves an item from the current HttpContext.Items collection.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <returns>The item value, or null if not found.</returns>
        object GetContextItem(string key);

        /// <summary>
        /// Completes the current request, preventing further processing.
        /// </summary>
        void CompleteRequest();
    }
}