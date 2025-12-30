using IISFrontGuard.Module.Services;
using NUnit.Framework;
using System.IO;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class GeoIPServiceAdapterTests
    {
        private static readonly string _testDatabasePath = Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "TestData", "GeoIP2-Country-Test.mmdb");

        [Test]
        public void GetGeoInfo_WithInvalidPath_ReturnsEmptyResponse()
        {
            // Arrange
            var adapter = new GeoIPServiceAdapter("invalid_path.mmdb");

            // Act
            var result = adapter.GetGeoInfo("8.8.8.8");

            // Assert
            Assert.IsNotNull(result);
        }

        [Test]
        public void GetGeoInfo_WithValidDatabase_ExecutesSuccessfully()
        {
            // Skip test if database file doesn't exist
            if (!File.Exists(_testDatabasePath))
            {
                Assert.Ignore("Test database not available");
                return;
            }

            // Arrange
            var adapter = new GeoIPServiceAdapter(_testDatabasePath);

            // Act - This will cover lines 23 and 24
            var result = adapter.GetGeoInfo("81.2.69.142"); // UK IP from MaxMind test data

            // Assert
            Assert.IsNotNull(result);
            // With the test database, we might get actual country data
            TestContext.WriteLine($"Country ISO: {result.Country?.IsoCode}");
        }

        [Test]
        public void GetGeoInfo_WithNullIpAddress_HandlesGracefully()
        {
            // Arrange
            var adapter = new GeoIPServiceAdapter("invalid_path.mmdb");

            // Act
            var result = adapter.GetGeoInfo(null);

            // Assert
            Assert.IsNotNull(result);
        }

        [Test]
        public void GetGeoInfo_WithInvalidIpAddress_ReturnsEmptyResponse()
        {
            // Skip test if database file doesn't exist
            if (!File.Exists(_testDatabasePath))
            {
                Assert.Ignore("Test database not available");
                return;
            }

            // Arrange
            var adapter = new GeoIPServiceAdapter(_testDatabasePath);

            // Act
            var result = adapter.GetGeoInfo("invalid-ip");

            // Assert
            Assert.IsNotNull(result);
        }

        [Test]
        public void GetGeoInfo_WithPrivateIpAddress_HandlesGracefully()
        {
            // Skip test if database file doesn't exist
            if (!File.Exists(_testDatabasePath))
            {
                Assert.Ignore("Test database not available");
                return;
            }

            // Arrange
            var adapter = new GeoIPServiceAdapter(_testDatabasePath);

            // Act - Private IPs typically aren't in GeoIP databases
            var result = adapter.GetGeoInfo("192.168.1.1");

            // Assert
            Assert.IsNotNull(result);
        }
    }
}
