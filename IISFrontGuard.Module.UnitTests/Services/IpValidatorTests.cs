using IISFrontGuard.Module.Abstractions;
using NUnit.Framework;
using System.Collections.Generic;
using System.Net;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class IpValidatorTests
    {
        [Test]
        public void Constructor_WithValidCidrList_InitializesSuccessfully()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24", "10.0.0.0/8" };

            // Act & Assert
            Assert.DoesNotThrow(() => new IpValidator(cidrList));
        }

        [Test]
        public void Constructor_WithEmptyCidrList_InitializesSuccessfully()
        {
            // Arrange
            var cidrList = new List<string>();

            // Act & Assert
            Assert.DoesNotThrow(() => new IpValidator(cidrList));
        }

        [Test]
        public void IsInIp_WithStringIpInRange_ReturnsTrue()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("192.168.1.100");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInIp_WithStringIpNotInRange_ReturnsFalse()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("192.168.2.100");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInIp_WithStringIpAtStartOfRange_ReturnsTrue()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("192.168.1.0");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInIp_WithStringIpAtEndOfRange_ReturnsTrue()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("192.168.1.255");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInIp_WithInvalidStringIp_ReturnsFalse()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("invalid-ip");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInIp_WithEmptyStringIp_ReturnsFalse()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInIp_WithIPAddressObjectInRange_ReturnsTrue()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);
            var ip = IPAddress.Parse("192.168.1.100");

            // Act
            var result = validator.IsInIp(ip);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInIp_WithIPAddressObjectNotInRange_ReturnsFalse()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);
            var ip = IPAddress.Parse("192.168.2.100");

            // Act
            var result = validator.IsInIp(ip);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInIp_WithMultipleRanges_FirstRangeMatches_ReturnsTrue()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24", "10.0.0.0/8", "172.16.0.0/12" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("192.168.1.50");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInIp_WithMultipleRanges_MiddleRangeMatches_ReturnsTrue()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24", "10.0.0.0/8", "172.16.0.0/12" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("10.5.10.15");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInIp_WithMultipleRanges_LastRangeMatches_ReturnsTrue()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24", "10.0.0.0/8", "172.16.0.0/12" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("172.20.5.10");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInIp_WithMultipleRanges_NoRangeMatches_ReturnsFalse()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24", "10.0.0.0/8", "172.16.0.0/12" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("8.8.8.8");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInIp_WithEmptyNetworkList_ReturnsFalse()
        {
            // Arrange
            var cidrList = new List<string>();
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("192.168.1.100");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInIp_WithSingleIpCidr_MatchesExactly()
        {
            // Arrange - /32 means single IP
            var cidrList = new List<string> { "192.168.1.100/32" };
            var validator = new IpValidator(cidrList);

            // Act
            var resultMatch = validator.IsInIp("192.168.1.100");
            var resultNoMatch = validator.IsInIp("192.168.1.101");

            // Assert
            Assert.IsTrue(resultMatch);
            Assert.IsFalse(resultNoMatch);
        }

        [Test]
        public void IsInIp_WithLargeCidrBlock_Class8_WorksCorrectly()
        {
            // Arrange
            var cidrList = new List<string> { "10.0.0.0/8" };
            var validator = new IpValidator(cidrList);

            // Act
            var result1 = validator.IsInIp("10.0.0.0");
            var result2 = validator.IsInIp("10.255.255.255");
            var result3 = validator.IsInIp("11.0.0.0");

            // Assert
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsFalse(result3);
        }

        [Test]
        public void IsInIp_WithPrivateIpRanges_IdentifiesCorrectly()
        {
            // Arrange - All private IP ranges
            var cidrList = new List<string>
            {
                "10.0.0.0/8",        // Class A private
                "172.16.0.0/12",     // Class B private
                "192.168.0.0/16"     // Class C private
            };
            var validator = new IpValidator(cidrList);

            // Act
            var result1 = validator.IsInIp("10.1.1.1");
            var result2 = validator.IsInIp("172.16.0.1");
            var result3 = validator.IsInIp("192.168.1.1");
            var result4 = validator.IsInIp("8.8.8.8"); // Public IP

            // Assert
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsTrue(result3);
            Assert.IsFalse(result4);
        }

        [Test]
        public void IsInIp_WithMixedIPv4AndIPv6Ranges_WorksCorrectly()
        {
            // Arrange
            var cidrList = new List<string>
            {
                "192.168.1.0/24",
                "2001:db8::/32"
            };
            var validator = new IpValidator(cidrList);

            // Act
            var ipv4InRange = validator.IsInIp("192.168.1.50");
            var ipv4OutRange = validator.IsInIp("192.168.2.50");

            // Assert
            Assert.IsTrue(ipv4InRange);
            Assert.IsFalse(ipv4OutRange);
        }

        [Test]
        public void IsInIp_WithWhitespaceIp_ReturnsFalse()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.0/24" };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("   ");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsInIp_WithOverlappingRanges_StillReturnsTrue()
        {
            // Arrange
            var cidrList = new List<string>
            {
                "192.168.0.0/16",    // Larger range
                "192.168.1.0/24"     // Smaller range within larger
            };
            var validator = new IpValidator(cidrList);

            // Act
            var result = validator.IsInIp("192.168.1.100");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsInIp_WithCidrSlash0_MatchesAllIPv4()
        {
            // Arrange
            var cidrList = new List<string> { "0.0.0.0/0" };
            var validator = new IpValidator(cidrList);

            // Act
            var result1 = validator.IsInIp("1.2.3.4");
            var result2 = validator.IsInIp("255.255.255.255");
            var result3 = validator.IsInIp("192.168.1.1");

            // Assert
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsTrue(result3);
        }

        [Test]
        public void IsInIp_WithBoundaryIpAddresses_WorksCorrectly()
        {
            // Arrange
            var cidrList = new List<string> { "192.168.1.128/25" }; // .128 to .255
            var validator = new IpValidator(cidrList);

            // Act
            var justBefore = validator.IsInIp("192.168.1.127");
            var firstIn = validator.IsInIp("192.168.1.128");
            var lastIn = validator.IsInIp("192.168.1.255");
            var justAfter = validator.IsInIp("192.168.2.0");

            // Assert
            Assert.IsFalse(justBefore);
            Assert.IsTrue(firstIn);
            Assert.IsTrue(lastIn);
            Assert.IsFalse(justAfter);
        }
    }
}
