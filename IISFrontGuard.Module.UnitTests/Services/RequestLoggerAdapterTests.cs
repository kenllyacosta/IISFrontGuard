using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Services;
using NUnit.Framework;
using System;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class RequestLoggerAdapterTests
    {
        private RequestLoggerAdapter _adapter;
        private const string TestConnectionString = "Server=(localdb)\\mssqllocaldb;Database=IISFrontGuardTest;Integrated Security=true;";
        private const string TestEncryptionKey = "1234567890123456";

        [SetUp]
        public void SetUp()
        {
            _adapter = new RequestLoggerAdapter();
        }
        
        [Test]
        public void Encrypt_WithValidInput_ReturnsEncryptedString()
        {
            // Arrange
            var clearText = "SensitiveData123";

            // Act
            var result = _adapter.Encrypt(clearText, TestEncryptionKey);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            Assert.AreNotEqual(clearText, result, "Encrypted text should differ from clear text");
        }

        [Test]
        public void Encrypt_WithEmptyString_ReturnsResult()
        {
            // Arrange
            var clearText = "";

            // Act
            var result = _adapter.Encrypt(clearText, TestEncryptionKey);

            // Assert
            Assert.IsNotNull(result);
        }

        [Test]
        public void Decrypt_WithValidInput_ReturnsDecryptedString()
        {
            // Arrange
            var originalText = "SecretData456";
            var encrypted = _adapter.Encrypt(originalText, TestEncryptionKey);

            // Act
            var result = _adapter.Decrypt(encrypted, TestEncryptionKey);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(originalText, result, "Decrypted text should match original");
        }

        [Test]
        public void EncryptDecrypt_RoundTrip_PreservesOriginalValue()
        {
            // Arrange
            var originalText = "RoundTripTest_!@#$%^&*()_+{}[]|:;<>?,./";

            // Act
            var encrypted = _adapter.Encrypt(originalText, TestEncryptionKey);
            var decrypted = _adapter.Decrypt(encrypted, TestEncryptionKey);

            // Assert
            Assert.AreEqual(originalText, decrypted, "Round-trip encryption/decryption should preserve original value");
        }

        [Test]
        public void Stop_WhenCalled_ExecutesWithoutError()
        {
            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => _adapter.Stop());
        }

        [Test]
        public void Stop_CalledMultipleTimes_ExecutesWithoutError()
        {
            // Act & Assert - Should not throw even when called multiple times
            Assert.DoesNotThrow(() => 
            {
                _adapter.Stop();
                _adapter.Stop();
                _adapter.Stop();
            });
        }

        [Test]
        public void Constructor_CreatesInstance_Successfully()
        {
            // Act
            var adapter = new RequestLoggerAdapter();

            // Assert
            Assert.IsNotNull(adapter);
            Assert.IsInstanceOf<IRequestLogger>(adapter);
        }

        [Test]
        public void Adapter_ImplementsIRequestLogger_Interface()
        {
            // Assert
            Assert.IsInstanceOf<IRequestLogger>(_adapter);
        }

        // Helper methods to create mock HttpRequest objects
        private HttpRequest CreateMockHttpRequest()
        {
            // Create a minimal HttpRequest for testing
            // Note: HttpRequest is difficult to mock without HttpContext
            try
            {
                var request = new HttpRequest("test.aspx", "http://example.com/test.aspx", "");
                return request;
            }
            catch
            {
                // If HttpRequest creation fails in test context, return null
                // The actual Enqueue method will handle this according to its implementation
                return null;
            }
        }

        private HttpRequest CreateMockHttpRequestWithBody(string body)
        {
            try
            {
                var request = new HttpRequest("test.aspx", "http://example.com/test.aspx", body);
                return request;
            }
            catch
            {
                return null;
            }
        }
    }
}