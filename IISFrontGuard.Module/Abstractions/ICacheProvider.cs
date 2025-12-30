using System;
using System.Web.Caching;

namespace IISFrontGuard.Module.Abstractions
{
    /// <summary>
    /// Defines an abstraction for cache operations to enable testability and flexibility.
    /// </summary>
    public interface ICacheProvider
    {
        /// <summary>
        /// Retrieves an item from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns>The cached object, or null if not found.</returns>
        object Get(string key);

        /// <summary>
        /// Inserts an item into the cache with the specified expiration policy.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The object to cache.</param>
        /// <param name="dependencies">Cache dependencies.</param>
        /// <param name="absoluteExpiration">Absolute expiration time.</param>
        /// <param name="slidingExpiration">Sliding expiration timespan.</param>
        void Insert(string key, object value, CacheDependency dependencies, DateTime absoluteExpiration, TimeSpan slidingExpiration);

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        void Remove(string key);
    }
}
