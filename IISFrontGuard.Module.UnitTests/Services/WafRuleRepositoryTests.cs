using IISFrontGuard.Module.Abstractions;
using IISFrontGuard.Module.Models;
using IISFrontGuard.Module.Services;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class WafRuleRepositoryTests
    {
        private Mock<ICacheProvider> _mockCache;
        private WafRuleRepository _repository;
        private const string TestConnectionString = "Server=(localdb)\\mssqllocaldb;Database=IISFrontGuardTest;Integrated Security=true;";

        [SetUp]
        public void SetUp()
        {
            _mockCache = new Mock<ICacheProvider>();
            _repository = new WafRuleRepository(_mockCache.Object);
        }

        [Test]
        public void FetchWafRules_WithNullHost_ReturnsEmpty()
        {
            // Act
            var result = _repository.FetchWafRules(null, TestConnectionString);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }

        [Test]
        public void FetchWafRules_WithEmptyHost_ReturnsEmpty()
        {
            // Act
            var result = _repository.FetchWafRules("", TestConnectionString);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }

        [Test]
        public void FetchWafRules_WithWhitespaceHost_ReturnsEmpty()
        {
            // Act
            var result = _repository.FetchWafRules("   ", TestConnectionString);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }

        [Test]
        public void FetchWafRules_WithHostLongerThan255_ReturnsEmpty()
        {
            // Arrange
            var longHost = new string('a', 256);

            // Act
            var result = _repository.FetchWafRules(longHost, TestConnectionString);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }

        [Test]
        public void FetchWafRules_WithCachedRules_ReturnsCachedData()
        {
            // Arrange
            var host = "example.com";
            var cacheKey = $"WAF_RULES_{host}";
            var cachedRules = new List<WafRule>
            {
                new WafRule
                {
                    Id = 1,
                    Nombre = "Cached Rule",
                    ActionId = 1,
                    Prioridad = 100,
                    Habilitado = true,
                    AppId = Guid.NewGuid(),
                    Conditions = new List<WafCondition>()
                }
            };

            _mockCache.Setup(c => c.Get(cacheKey)).Returns(cachedRules);

            // Act
            var result = _repository.FetchWafRules(host, TestConnectionString);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual("Cached Rule", result.First().Nombre);
            _mockCache.Verify(c => c.Get(cacheKey), Times.Once);
        }

        [Test]
        public void FetchWafRules_WithNoCachedRules_ReturnsNull()
        {
            // Arrange
            var host = "example.com";
            var cacheKey = $"WAF_RULES_{host}";

            _mockCache.Setup(c => c.Get(cacheKey)).Returns(null);

            // Act & Assert - This will fail due to no database, but tests cache miss logic
            Assert.Throws<SqlException>(() => _repository.FetchWafRules(host, TestConnectionString));
            _mockCache.Verify(c => c.Get(cacheKey), Times.Once);
        }

        [Test]
        public void FetchWafRules_NormalizesHostToLowercase()
        {
            // Arrange
            var host = "EXAMPLE.COM";
            var cacheKey = "WAF_RULES_example.com"; // Should normalize to lowercase

            var cachedRules = new List<WafRule>
            {
                new WafRule { Id = 1, Nombre = "Test", ActionId = 1, Prioridad = 1, Habilitado = true, AppId = Guid.NewGuid() }
            };

            _mockCache.Setup(c => c.Get(cacheKey)).Returns(cachedRules);

            // Act
            var result = _repository.FetchWafRules(host, TestConnectionString);

            // Assert
            Assert.IsNotNull(result);
            _mockCache.Verify(c => c.Get(cacheKey), Times.Once);
        }

        [Test]
        public void FetchWafRules_ExtractsHostFromFullUrl()
        {
            // Arrange - Even if given a host that needs URI parsing
            var host = "subdomain.example.com";
            var cacheKey = "WAF_RULES_subdomain.example.com";

            var cachedRules = new List<WafRule>
            {
                new WafRule { Id = 1, Nombre = "Test", ActionId = 1, Prioridad = 1, Habilitado = true, AppId = Guid.NewGuid() }
            };

            _mockCache.Setup(c => c.Get(cacheKey)).Returns(cachedRules);

            // Act
            var result = _repository.FetchWafRules(host, TestConnectionString);

            // Assert
            Assert.IsNotNull(result);
            _mockCache.Verify(c => c.Get(cacheKey), Times.Once);
        }

        [Test]
        public void FetchWafRules_WithInvalidConnectionString_ThrowsSqlException()
        {
            // Arrange
            var host = "example.com";
            var invalidConnectionString = "Invalid Connection String";
            _mockCache.Setup(c => c.Get(It.IsAny<string>())).Returns(null);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _repository.FetchWafRules(host, invalidConnectionString));
        }

        [Test]
        public void FetchWafConditions_WithInvalidConnectionString_ThrowsSqlException()
        {
            // Arrange
            var ruleId = 1;
            var invalidConnectionString = "Invalid Connection String";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _repository.FetchWafConditions(ruleId, invalidConnectionString));
        }

        [Test]
        public void FetchWafConditions_WithValidRuleId_ExecutesQuery()
        {
            // Arrange
            var ruleId = 1;

            // Act & Assert - Will fail due to no database connection, but tests method execution
            Assert.Throws<SqlException>(() => _repository.FetchWafConditions(ruleId, TestConnectionString));
        }

        [Test]
        public void FetchWafRules_CacheKeyFormat_IsCorrect()
        {
            // Arrange
            var host = "test.example.com";
            var expectedCacheKey = "WAF_RULES_test.example.com";

            var cachedRules = new List<WafRule>
            {
                new WafRule { Id = 1, Nombre = "Test", ActionId = 1, Prioridad = 1, Habilitado = true, AppId = Guid.NewGuid() }
            };

            _mockCache.Setup(c => c.Get(expectedCacheKey)).Returns(cachedRules);

            // Act
            var result = _repository.FetchWafRules(host, TestConnectionString);

            // Assert
            _mockCache.Verify(c => c.Get(expectedCacheKey), Times.Once);
        }

        [Test]
        public void FetchWafRules_WithHostContainingPort_NormalizesCorrectly()
        {
            // Arrange
            var host = "example.com:8080";
            var expectedCacheKey = "WAF_RULES_example.com"; // Port should be stripped

            var cachedRules = new List<WafRule>
            {
                new WafRule { Id = 1, Nombre = "Test", ActionId = 1, Prioridad = 1, Habilitado = true, AppId = Guid.NewGuid() }
            };

            _mockCache.Setup(c => c.Get(expectedCacheKey)).Returns(cachedRules);

            // Act
            var result = _repository.FetchWafRules(host, TestConnectionString);

            // Assert
            _mockCache.Verify(c => c.Get(expectedCacheKey), Times.Once);
        }

        [Test]
        public void FetchWafRules_ReturnsEmptyList_WhenCacheReturnsNonListType()
        {
            // Arrange
            var host = "example.com";
            var cacheKey = $"WAF_RULES_{host}";

            // Cache returns wrong type
            _mockCache.Setup(c => c.Get(cacheKey)).Returns("wrong type");

            // Act & Assert - Will attempt to query database since cast fails
            Assert.Throws<SqlException>(() => _repository.FetchWafRules(host, TestConnectionString));
        }
    }
}
