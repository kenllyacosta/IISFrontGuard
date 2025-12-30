using IISFrontGuard.Module.Services;
using NUnit.Framework;
using System;
using System.IO;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class HttpContextAccessorTests
    {
        private HttpContextAccessor _accessor;

        [SetUp]
        public void SetUp()
        {
            _accessor = new HttpContextAccessor();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up HttpContext after each test
            HttpContext.Current = null;
        }

        [Test]
        public void Constructor_InitializesSuccessfully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new HttpContextAccessor());
        }

        [Test]
        public void Current_WhenHttpContextIsNull_ReturnsNull()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = _accessor.Current;

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void Current_WhenHttpContextExists_ReturnsContext()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;

            // Act
            var result = _accessor.Current;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(context, result);
        }

        [Test]
        public void Current_ReflectsCurrentHttpContext()
        {
            // Arrange
            var context1 = CreateHttpContext();
            var context2 = CreateHttpContext();

            // Act & Assert - First context
            HttpContext.Current = context1;
            Assert.AreSame(context1, _accessor.Current);

            // Act & Assert - Second context
            HttpContext.Current = context2;
            Assert.AreSame(context2, _accessor.Current);
        }

        [Test]
        public void SetContextItem_WhenHttpContextIsNull_DoesNotThrow()
        {
            // Arrange
            HttpContext.Current = null;

            // Act & Assert
            Assert.DoesNotThrow(() => _accessor.SetContextItem("test-key", "test-value"));
        }

        [Test]
        public void SetContextItem_WhenHttpContextExists_SetsItem()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "test-key";
            var value = "test-value";

            // Act
            _accessor.SetContextItem(key, value);

            // Assert
            Assert.AreEqual(value, context.Items[key]);
        }

        [Test]
        public void SetContextItem_WithNullValue_SetsNullInItems()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "null-value-key";

            // Act
            _accessor.SetContextItem(key, null);

            // Assert
            Assert.IsTrue(context.Items.Contains(key));
            Assert.IsNull(context.Items[key]);
        }

        [Test]
        public void SetContextItem_WithEmptyKey_SetsItem()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "";
            var value = "empty-key-value";

            // Act
            _accessor.SetContextItem(key, value);

            // Assert
            Assert.AreEqual(value, context.Items[key]);
        }

        [Test]
        public void SetContextItem_OverwritesExistingValue()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "overwrite-key";
            var originalValue = "original";
            var newValue = "new";
            context.Items[key] = originalValue;

            // Act
            _accessor.SetContextItem(key, newValue);

            // Assert
            Assert.AreEqual(newValue, context.Items[key]);
        }

        [Test]
        public void SetContextItem_WithDifferentDataTypes_StoresCorrectly()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;

            // Act
            _accessor.SetContextItem("string-key", "string-value");
            _accessor.SetContextItem("int-key", 42);
            _accessor.SetContextItem("bool-key", true);
            _accessor.SetContextItem("datetime-key", DateTime.UtcNow);
            _accessor.SetContextItem("object-key", new { Name = "Test", Value = 123 });

            // Assert
            Assert.AreEqual("string-value", context.Items["string-key"]);
            Assert.AreEqual(42, context.Items["int-key"]);
            Assert.IsInstanceOf<DateTime>(context.Items["datetime-key"]);
            Assert.IsNotNull(context.Items["object-key"]);
        }

        [Test]
        public void SetContextItem_WithSpecialCharactersInKey_SetsItem()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "key:with:special:chars!@#$%";
            var value = "special-value";

            // Act
            _accessor.SetContextItem(key, value);

            // Assert
            Assert.AreEqual(value, context.Items[key]);
        }

        [Test]
        public void GetContextItem_WhenHttpContextIsNull_ReturnsNull()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = _accessor.GetContextItem("test-key");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetContextItem_WithExistingKey_ReturnsValue()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "existing-key";
            var value = "existing-value";
            context.Items[key] = value;

            // Act
            var result = _accessor.GetContextItem(key);

            // Assert
            Assert.AreEqual(value, result);
        }

        [Test]
        public void GetContextItem_WithNonExistentKey_ReturnsNull()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;

            // Act
            var result = _accessor.GetContextItem("non-existent-key");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetContextItem_AfterSetContextItem_ReturnsSetValue()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "round-trip-key";
            var value = "round-trip-value";

            // Act
            _accessor.SetContextItem(key, value);
            var result = _accessor.GetContextItem(key);

            // Assert
            Assert.AreEqual(value, result);
        }

        [Test]
        public void GetContextItem_WithDifferentDataTypes_ReturnsCorrectType()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var dateValue = DateTime.UtcNow;

            _accessor.SetContextItem("string-key", "string-value");
            _accessor.SetContextItem("int-key", 42);
            _accessor.SetContextItem("datetime-key", dateValue);

            // Act & Assert
            Assert.AreEqual("string-value", _accessor.GetContextItem("string-key"));
            Assert.AreEqual(42, _accessor.GetContextItem("int-key"));
            Assert.AreEqual(dateValue, _accessor.GetContextItem("datetime-key"));
        }

        [Test]
        public void GetContextItem_WithEmptyKey_ReturnsValue()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "";
            var value = "empty-key-value";
            context.Items[key] = value;

            // Act
            var result = _accessor.GetContextItem(key);

            // Assert
            Assert.AreEqual(value, result);
        }

        [Test]
        public void CompleteRequest_WhenHttpContextIsNull_DoesNotThrow()
        {
            // Arrange
            HttpContext.Current = null;

            // Act & Assert
            Assert.DoesNotThrow(() => _accessor.CompleteRequest());
        }

        [Test]
        public void CompleteRequest_WhenHttpContextExists_CallsCompleteRequest()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;

            // Act & Assert
            // CompleteRequest should not throw, but we can't easily verify it was called
            // without a full ASP.NET pipeline. We just verify it doesn't throw.
            Assert.DoesNotThrow(() => _accessor.CompleteRequest());
        }

        [Test]
        public void CompleteRequest_WhenApplicationInstanceIsNull_DoesNotThrow()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            // ApplicationInstance is typically null in unit tests

            // Act & Assert
            Assert.DoesNotThrow(() => _accessor.CompleteRequest());
        }

        [Test]
        public void ContextItems_MultipleOperations_WorkIndependently()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;

            // Act
            _accessor.SetContextItem("key1", "value1");
            _accessor.SetContextItem("key2", "value2");
            _accessor.SetContextItem("key3", "value3");
            var result1 = _accessor.GetContextItem("key1");
            var result2 = _accessor.GetContextItem("key2");
            var result3 = _accessor.GetContextItem("key3");
            var resultNonExistent = _accessor.GetContextItem("key4");

            // Assert
            Assert.AreEqual("value1", result1);
            Assert.AreEqual("value2", result2);
            Assert.AreEqual("value3", result3);
            Assert.IsNull(resultNonExistent);
        }

        [Test]
        public void SetContextItem_WithLargeObject_StoresCorrectly()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "large-object-key";
            var largeArray = new byte[1024 * 100]; // 100KB
            for (int i = 0; i < largeArray.Length; i++)
            {
                largeArray[i] = (byte)(i % 256);
            }

            // Act
            _accessor.SetContextItem(key, largeArray);

            // Assert
            var result = _accessor.GetContextItem(key) as byte[];
            Assert.IsNotNull(result);
            Assert.AreEqual(largeArray.Length, result.Length);
        }

        [Test]
        public void Current_MultipleCalls_ReturnsSameInstance()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;

            // Act
            var result1 = _accessor.Current;
            var result2 = _accessor.Current;

            // Assert
            Assert.AreSame(result1, result2);
        }

        [Test]
        public void SetContextItem_AfterContextCleared_DoesNotAffectNewContext()
        {
            // Arrange
            var context1 = CreateHttpContext();
            HttpContext.Current = context1;
            _accessor.SetContextItem("test-key", "value1");

            // Act - Switch to new context
            var context2 = CreateHttpContext();
            HttpContext.Current = context2;
            var result = _accessor.GetContextItem("test-key");

            // Assert
            Assert.IsNull(result); // New context shouldn't have the old value
        }

        [Test]
        public void GetContextItem_CaseSensitiveKeys_ReturnsDifferentValues()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            context.Items["TestKey"] = "value1";
            context.Items["testkey"] = "value2";

            // Act
            var result1 = _accessor.GetContextItem("TestKey");
            var result2 = _accessor.GetContextItem("testkey");

            // Assert
            Assert.AreEqual("value1", result1);
            Assert.AreEqual("value2", result2);
        }

        [Test]
        public void SetContextItem_WithComplexObject_PreservesObjectReference()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "complex-object";
            var complexObject = new TestComplexObject { Name = "Test", Value = 123, Items = new[] { 1, 2, 3 } };

            // Act
            _accessor.SetContextItem(key, complexObject);
            var result = _accessor.GetContextItem(key) as TestComplexObject;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(complexObject, result);
            Assert.AreEqual("Test", result.Name);
            Assert.AreEqual(123, result.Value);
            Assert.AreEqual(3, result.Items.Length);
        }

        [Test]
        public void GetContextItem_WithNullValueStored_ReturnsNull()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;
            var key = "null-stored-key";
            context.Items[key] = null;

            // Act
            var result = _accessor.GetContextItem(key);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void SetContextItem_ConcurrentKeys_DoNotInterfere()
        {
            // Arrange
            var context = CreateHttpContext();
            HttpContext.Current = context;

            // Act
            _accessor.SetContextItem("user-id", "123");
            _accessor.SetContextItem("session-id", "abc");
            _accessor.SetContextItem("request-id", "xyz");

            // Assert
            Assert.AreEqual("123", _accessor.GetContextItem("user-id"));
            Assert.AreEqual("abc", _accessor.GetContextItem("session-id"));
            Assert.AreEqual("xyz", _accessor.GetContextItem("request-id"));
        }

        private HttpContext CreateHttpContext()
        {
            var request = new HttpRequest("", "http://localhost/", "");
            var response = new HttpResponse(new StringWriter());
            var context = new HttpContext(request, response);
            return context;
        }

        private class TestComplexObject
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public int[] Items { get; set; }
        }
    }
}
