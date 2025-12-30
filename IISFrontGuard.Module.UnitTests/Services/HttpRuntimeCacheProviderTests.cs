using IISFrontGuard.Module.Services;
using NUnit.Framework;
using System;
using System.Web;
using System.Web.Caching;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class HttpRuntimeCacheProviderTests
    {
        private HttpRuntimeCacheProvider _provider;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Initialize HttpRuntime if not already initialized
            // This is necessary because HttpRuntime.Cache requires ASP.NET context
            if (HttpRuntime.Cache == null)
            {
                // Create a minimal hosting environment for testing
                try
                {
                    var tempPath = System.IO.Path.GetTempPath();
                    var appDomain = AppDomain.CurrentDomain;
                    appDomain.SetData(".appDomain", "*");
                    appDomain.SetData(".appPath", tempPath);
                    appDomain.SetData(".appVPath", "/");
                    appDomain.SetData(".hostingVirtualPath", "/");
                    appDomain.SetData(".hostingInstallDir", tempPath);
                }
                catch
                {
                    // If initialization fails, tests will skip as needed
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            // Clear any existing cache entries before each test
            if (HttpRuntime.Cache != null)
            {
                var enumerator = HttpRuntime.Cache.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    HttpRuntime.Cache.Remove((string)enumerator.Key);
                }
            }

            _provider = new HttpRuntimeCacheProvider();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up cache after each test
            if (HttpRuntime.Cache != null)
            {
                var enumerator = HttpRuntime.Cache.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    HttpRuntime.Cache.Remove((string)enumerator.Key);
                }
            }
        }

        [Test]
        public void Constructor_InitializesSuccessfully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new HttpRuntimeCacheProvider());
        }

        [Test]
        public void Constructor_InitializesWithNonNullCache()
        {
            // Arrange & Act
            var provider = new HttpRuntimeCacheProvider();

            // Assert
            // We can't directly access _cache, but we can verify the provider works
            Assert.DoesNotThrow(() => provider.Get("test-key"));
        }

        [Test]
        public void Get_WithNonExistentKey_ReturnsNull()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "non-existent-key";

            // Act
            var result = _provider.Get(key);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void Get_WithExistingKey_ReturnsValue()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "test-key";
            var value = "test-value";
            HttpRuntime.Cache.Insert(key, value);

            // Act
            var result = _provider.Get(key);

            // Assert
            Assert.AreEqual(value, result);
        }

        [Test]
        public void Get_WithDifferentValueTypes_ReturnsCorrectType()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange - Test with int
            var intKey = "int-key";
            var intValue = 42;
            HttpRuntime.Cache.Insert(intKey, intValue);

            // Act
            var intResult = _provider.Get(intKey);

            // Assert
            Assert.AreEqual(intValue, intResult);
            Assert.IsInstanceOf<int>(intResult);
        }

        [Test]
        public void Get_WithComplexObject_ReturnsObject()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "complex-key";
            var value = new { Name = "Test", Value = 123 };
            HttpRuntime.Cache.Insert(key, value);

            // Act
            var result = _provider.Get(key);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(value, result);
        }

        [Test]
        public void Insert_WithValidParameters_AddsToCache()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "insert-key";
            var value = "insert-value";

            // Act
            _provider.Insert(key, value, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Assert
            var cachedValue = HttpRuntime.Cache.Get(key);
            Assert.AreEqual(value, cachedValue);
        }

        [Test]
        public void Insert_WithAbsoluteExpiration_AddsToCache()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "expire-key";
            var value = "expire-value";
            var expiration = DateTime.UtcNow.AddMinutes(5);

            // Act
            _provider.Insert(key, value, null, expiration, Cache.NoSlidingExpiration);

            // Assert
            var cachedValue = _provider.Get(key);
            Assert.AreEqual(value, cachedValue);
        }

        [Test]
        public void Insert_WithSlidingExpiration_AddsToCache()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "sliding-key";
            var value = "sliding-value";
            var slidingExpiration = TimeSpan.FromMinutes(5);

            // Act
            _provider.Insert(key, value, null, Cache.NoAbsoluteExpiration, slidingExpiration);

            // Assert
            var cachedValue = _provider.Get(key);
            Assert.AreEqual(value, cachedValue);
        }

        [Test]
        public void Insert_OverwritesExistingValue()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "overwrite-key";
            var originalValue = "original";
            var newValue = "new";
            _provider.Insert(key, originalValue, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Act
            _provider.Insert(key, newValue, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Assert
            var cachedValue = _provider.Get(key);
            Assert.AreEqual(newValue, cachedValue);
        }

        [Test]
        public void Insert_WithDifferentDataTypes_StoresCorrectly()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange & Act
            _provider.Insert("string-key", "string-value", null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);
            _provider.Insert("int-key", 42, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);
            _provider.Insert("bool-key", true, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);
            _provider.Insert("datetime-key", DateTime.UtcNow, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Assert
            Assert.AreEqual("string-value", _provider.Get("string-key"));
            Assert.AreEqual(42, _provider.Get("int-key"));
            Assert.IsInstanceOf<DateTime>(_provider.Get("datetime-key"));
        }

        [Test]
        public void Remove_WithExistingKey_RemovesFromCache()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "remove-key";
            var value = "remove-value";
            _provider.Insert(key, value, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Act
            _provider.Remove(key);

            // Assert
            var cachedValue = _provider.Get(key);
            Assert.IsNull(cachedValue);
        }

        [Test]
        public void Remove_WithNonExistentKey_DoesNotThrow()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "non-existent-remove-key";

            // Act & Assert
            Assert.DoesNotThrow(() => _provider.Remove(key));
        }

        [Test]
        public void Remove_ReturnsRemovedValue()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "return-remove-key";
            var value = "return-remove-value";
            _provider.Insert(key, value, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Act
            _provider.Remove(key);

            // Assert
            // HttpRuntime.Cache.Remove returns the removed object, but our interface is void
            // Just verify it's gone
            Assert.IsNull(_provider.Get(key));
        }

        [Test]
        public void CacheOperations_WithMultipleKeys_WorkIndependently()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key1 = "multi-key-1";
            var key2 = "multi-key-2";
            var key3 = "multi-key-3";
            var value1 = "value-1";
            var value2 = "value-2";
            var value3 = "value-3";

            // Act
            _provider.Insert(key1, value1, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);
            _provider.Insert(key2, value2, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);
            _provider.Insert(key3, value3, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);
            _provider.Remove(key2);

            // Assert
            Assert.AreEqual(value1, _provider.Get(key1));
            Assert.IsNull(_provider.Get(key2));
            Assert.AreEqual(value3, _provider.Get(key3));
        }

        [Test]
        public void Insert_WithCacheDependency_StoresCorrectly()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "dependency-key";
            var value = "dependency-value";
            CacheDependency dependency = null; // Can be null for this test

            // Act
            _provider.Insert(key, value, dependency, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Assert
            var cachedValue = _provider.Get(key);
            Assert.AreEqual(value, cachedValue);
        }

        [Test]
        public void Get_AfterInsertAndRemove_ReturnsNull()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "lifecycle-key";
            var value = "lifecycle-value";

            // Act
            _provider.Insert(key, value, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);
            Assert.AreEqual(value, _provider.Get(key)); // Verify it was added
            _provider.Remove(key);
            var result = _provider.Get(key);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void Insert_WithEmptyStringKey_AllowsEmptyKey()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "";
            var value = "empty-key-value";

            // Act & Assert
            // HttpRuntime.Cache might allow empty strings, but it's not recommended
            // This test documents the behavior
            Assert.DoesNotThrow(() =>
                _provider.Insert(key, value, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration));
        }

        [Test]
        public void Insert_WithLongKey_HandlesCorrectly()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = new string('a', 500); // Long key
            var value = "long-key-value";

            // Act
            _provider.Insert(key, value, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Assert
            Assert.AreEqual(value, _provider.Get(key));
        }

        [Test]
        public void Insert_WithSpecialCharactersInKey_HandlesCorrectly()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "key:with:special:chars!@#$%";
            var value = "special-char-value";

            // Act
            _provider.Insert(key, value, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Assert
            Assert.AreEqual(value, _provider.Get(key));
        }

        [Test]
        public void Insert_WithLargeObject_StoresCorrectly()
        {
            if (HttpRuntime.Cache == null)
            {
                Assert.Ignore("HttpRuntime.Cache is not available in this test environment");
                return;
            }

            // Arrange
            var key = "large-object-key";
            var largeArray = new byte[1024 * 100]; // 100KB
            for (int i = 0; i < largeArray.Length; i++)
            {
                largeArray[i] = (byte)(i % 256);
            }

            // Act
            _provider.Insert(key, largeArray, null, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);

            // Assert
            var cachedValue = _provider.Get(key) as byte[];
            Assert.IsNotNull(cachedValue);
            Assert.AreEqual(largeArray.Length, cachedValue.Length);
        }
    }
}
