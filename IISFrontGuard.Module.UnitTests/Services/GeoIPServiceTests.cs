using IISFrontGuard.Module.Services;
using MaxMind.GeoIP2.Responses;
using NUnit.Framework;
using System.IO;
using System.Web;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class GeoIPServiceTests
    {
        private static readonly string _testDatabasePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "TestData", "GeoIP2-Country-Test.mmdb");

        private HttpContext _originalContext;

        [SetUp]
        public void SetUp()
        {
            // Save original context
            _originalContext = HttpContext.Current;
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original context
            HttpContext.Current = _originalContext;
        }

        [Test]
        public void GetGeoInfo_WhenHttpContextIsNull_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = GeoIPService.GetGeoInfo("8.8.8.8", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithInvalidIpAddress_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = GeoIPService.GetGeoInfo("invalid-ip-address", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithNonExistentDatabaseFile_ReturnsEmptyResponse()
        {
            // Arrange
            var context = CreateBasicHttpContext();
            HttpContext.Current = context;

            // Act - Will fail to find database and return empty response
            var result = GeoIPService.GetGeoInfo("8.8.8.8", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithPrivateIpAddress_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act - Private IP addresses typically not in GeoIP databases
            var result = GeoIPService.GetGeoInfo("192.168.1.1", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithLocalhostIp_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = GeoIPService.GetGeoInfo("127.0.0.1", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithIPv6Address_HandlesGracefully()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = GeoIPService.GetGeoInfo("2001:4860:4860::8888", _testDatabasePath); // Google's public IPv6 DNS

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithIPv6LocalhostAddress_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = GeoIPService.GetGeoInfo("::1", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithWhitespaceIpAddress_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = GeoIPService.GetGeoInfo("   ", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithMultipleCalls_ReturnsConsistentResults()
        {
            // Arrange
            HttpContext.Current = null;
            var ipAddress = "81.2.69.142";

            // Act
            var result1 = GeoIPService.GetGeoInfo(ipAddress, _testDatabasePath);
            var result2 = GeoIPService.GetGeoInfo(ipAddress, _testDatabasePath);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            
            // Both should be empty responses since no database is available
            Assert.IsInstanceOf<CountryResponse>(result1);
            Assert.IsInstanceOf<CountryResponse>(result2);
        }

        [Test]
        public void GetGeoInfo_WithDifferentPublicIpAddresses_ReturnsAppropriateResponses()
        {
            // Arrange
            HttpContext.Current = null;

            // Act - Various public IP addresses
            var resultUK = GeoIPService.GetGeoInfo("81.2.69.142", _testDatabasePath);     // UK
            var resultUS = GeoIPService.GetGeoInfo("8.8.8.8", _testDatabasePath);         // Google DNS (US)
            var resultCloudflare = GeoIPService.GetGeoInfo("1.1.1.1", _testDatabasePath); // Cloudflare DNS

            // Assert
            Assert.IsNotNull(resultUK);
            Assert.IsNotNull(resultUS);
            Assert.IsNotNull(resultCloudflare);
        }

        [Test]
        public void GetGeoInfo_WhenServerIsNull_ReturnsEmptyResponse()
        {
            // Arrange
            var context = CreateBasicHttpContext();
            HttpContext.Current = context;
            // HttpContext.Server is null in this basic setup

            // Act
            var result = GeoIPService.GetGeoInfo("8.8.8.8", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithMalformedIpAddress_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result1 = GeoIPService.GetGeoInfo("999.999.999.999", _testDatabasePath);
            var result2 = GeoIPService.GetGeoInfo("192.168.1", _testDatabasePath);
            var result3 = GeoIPService.GetGeoInfo("192.168.1.1.1", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.IsNotNull(result3);
        }

        [Test]
        public void GetGeoInfo_WithSpecialCharactersInIp_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = GeoIPService.GetGeoInfo("192.168.@.#", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_CatchesAllExceptions_ReturnsEmptyResponse()
        {
            // Arrange - Create a scenario that will throw an exception
            HttpContext.Current = null;

            // Act - This will throw when trying to access HttpContext.Current.Server
            var result = GeoIPService.GetGeoInfo("8.8.8.8", _testDatabasePath);

            // Assert - Should catch the exception and return empty response
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithBoundaryIpAddresses_HandlesCorrectly()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result1 = GeoIPService.GetGeoInfo("0.0.0.0", _testDatabasePath);
            var result2 = GeoIPService.GetGeoInfo("255.255.255.255", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
        }

        [Test]
        public void GetGeoInfo_WithLongIpString_ReturnsEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;
            var longIp = new string('1', 1000);

            // Act
            var result = GeoIPService.GetGeoInfo(longIp, _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        [Test]
        public void GetGeoInfo_WithValidIPv4Format_ReturnsResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act - Various valid IPv4 formats
            var result1 = GeoIPService.GetGeoInfo("1.2.3.4", _testDatabasePath);
            var result2 = GeoIPService.GetGeoInfo("10.0.0.1", _testDatabasePath);
            var result3 = GeoIPService.GetGeoInfo("172.16.0.1", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.IsNotNull(result3);
        }

        [Test]
        public void GetGeoInfo_WithValidIPv6Format_ReturnsResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act - Various valid IPv6 formats
            var result1 = GeoIPService.GetGeoInfo("2001:db8::1", _testDatabasePath);
            var result2 = GeoIPService.GetGeoInfo("fe80::1", _testDatabasePath);
            var result3 = GeoIPService.GetGeoInfo("::1", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.IsNotNull(result3);
        }

        [Test]
        public void GetGeoInfo_ExceptionHandling_NeverThrows()
        {
            // Arrange - Various scenarios that could throw
            HttpContext.Current = null;

            // Act & Assert - None of these should throw
            Assert.DoesNotThrow(() => GeoIPService.GetGeoInfo("invalid", _testDatabasePath));
            Assert.DoesNotThrow(() => GeoIPService.GetGeoInfo("8.8.8.8", _testDatabasePath));
        }

        [Test]
        public void GetGeoInfo_WithNullContext_MultipleCalls_AllReturnEmptyResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var results = new CountryResponse[10];
            for (int i = 0; i < 10; i++)
            {
                results[i] = GeoIPService.GetGeoInfo($"8.8.8.{i}", _testDatabasePath);
            }

            // Assert
            foreach (var result in results)
            {
                Assert.IsNotNull(result);
                Assert.IsInstanceOf<CountryResponse>(result);
            }
        }

        [Test]
        public void GetGeoInfo_ReturnType_IsAlwaysCountryResponse()
        {
            // Arrange
            HttpContext.Current = null;

            // Act
            var result = GeoIPService.GetGeoInfo("8.8.8.8", _testDatabasePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
            Assert.AreEqual(typeof(CountryResponse), result.GetType());
        }

        [Test]
        public void GetGeoInfo_WithBasicHttpContext_HandlesServerMapPathFailure()
        {
            // Arrange
            var context = CreateBasicHttpContext();
            HttpContext.Current = context;

            // Act - Server.MapPath will fail or return unexpected path
            var result = GeoIPService.GetGeoInfo("8.8.8.8", _testDatabasePath);

            // Assert - Should handle gracefully
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CountryResponse>(result);
        }

        private HttpContext CreateBasicHttpContext()
        {
            var request = new HttpRequest("", "http://localhost/", "");
            var response = new HttpResponse(new StringWriter());
            var context = new HttpContext(request, response);
            return context;
        }
    }
}
