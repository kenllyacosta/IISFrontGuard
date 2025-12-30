using IISFrontGuard.Module.Abstractions;
using System;
using System.Web;
using System.Web.Caching;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Provides caching functionality using the ASP.NET HttpRuntime.Cache.
    /// </summary>
    public class HttpRuntimeCacheProvider : ICacheProvider
    {
        private readonly Cache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRuntimeCacheProvider"/> class.
        /// </summary>
        public HttpRuntimeCacheProvider()
        {
            _cache = HttpRuntime.Cache;
        }

        /// <summary>
        /// Retrieves an item from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns>The cached object, or null if not found.</returns>
        public object Get(string key)
        {
            return _cache.Get(key);
        }

        /// <summary>
        /// Inserts an item into the cache with the specified expiration policy.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The object to cache.</param>
        /// <param name="dependencies">Cache dependencies.</param>
        /// <param name="absoluteExpiration">Absolute expiration time.</param>
        /// <param name="slidingExpiration">Sliding expiration timespan.</param>
        public void Insert(string key, object value, CacheDependency dependencies, DateTime absoluteExpiration, TimeSpan slidingExpiration)
        {
            _cache.Insert(key, value, dependencies, absoluteExpiration, slidingExpiration);
        }

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        public void Remove(string key)
        {
            _cache.Remove(key);
        }
    }
}
