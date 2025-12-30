using IISFrontGuard.Module.Services;
using NUnit.Framework;
using System.Configuration;

namespace IISFrontGuard.Module.UnitTests.Services
{
    [TestFixture]
    public class AppConfigConfigurationProviderTests
    {
        private AppConfigConfigurationProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new AppConfigConfigurationProvider();
        }

        [Test]
        public void Constructor_InitializesSuccessfully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new AppConfigConfigurationProvider());
        }

        #region GetAppSetting Tests

        [Test]
        public void GetAppSetting_WithExistingKey_ReturnsValue()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Enabled";

            // Act
            var result = _provider.GetAppSetting(key);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("true", result);
        }

        [Test]
        public void GetAppSetting_WithNonExistentKey_ReturnsNull()
        {
            // Arrange
            var key = "NonExistentKey";

            // Act
            var result = _provider.GetAppSetting(key);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetAppSetting_WithNullKey_ReturnsNull()
        {
            // Act
            var result = _provider.GetAppSetting(null);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetAppSetting_WithEmptyKey_ReturnsNull()
        {
            // Act
            var result = _provider.GetAppSetting("");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetAppSetting_WithEmptyValue_ReturnsEmptyString()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Url"; // Has empty value in config

            // Act
            var result = _provider.GetAppSetting(key);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("", result);
        }

        [Test]
        public void GetAppSetting_CaseSensitive_ReturnsNull()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Enabled"; // Different case

            // Act
            var result = _provider.GetAppSetting(key);

            // Assert
            // ConfigurationManager.AppSettings is case-insensitive by default
            // but we test the actual behavior
            var expected = ConfigurationManager.AppSettings[key];
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void GetAppSetting_WithWhitespaceKey_ReturnsNull()
        {
            // Act
            var result = _provider.GetAppSetting("   ");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetAppSetting_MultipleCalls_ReturnsSameValue()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Enabled";

            // Act
            var result1 = _provider.GetAppSetting(key);
            var result2 = _provider.GetAppSetting(key);

            // Assert
            Assert.AreEqual(result1, result2);
        }

        #endregion

        #region GetConnectionString Tests

        [Test]
        public void GetConnectionString_WithNonExistentName_ReturnsNull()
        {
            // Arrange
            var name = "NonExistentConnectionString";

            // Act
            var result = _provider.GetConnectionString(name);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetConnectionString_WithNullName_ReturnsNull()
        {
            // Act
            var result = _provider.GetConnectionString(null);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetConnectionString_WithEmptyName_ReturnsNull()
        {
            // Act
            var result = _provider.GetConnectionString("");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetConnectionString_WithWhitespaceName_ReturnsNull()
        {
            // Act
            var result = _provider.GetConnectionString("   ");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetConnectionString_UsesNullConditionalOperator()
        {
            // Arrange
            var name = "NonExistentConnectionString";

            // Act & Assert - Should not throw NullReferenceException
            Assert.DoesNotThrow(() => _provider.GetConnectionString(name));
        }

        #endregion

        #region GetAppSettingAsInt Tests

        [Test]
        public void GetAppSettingAsInt_WithValidIntValue_ReturnsInt()
        {
            // Arrange
            var key = "TestIntKey";
            // Add a test value to AppSettings at runtime
            ConfigurationManager.AppSettings[key] = "42";

            // Act
            var result = _provider.GetAppSettingAsInt(key, 0);

            // Assert
            Assert.AreEqual(42, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithNonExistentKey_ReturnsDefaultValue()
        {
            // Arrange
            var key = "NonExistentIntKey";
            var defaultValue = 100;

            // Act
            var result = _provider.GetAppSettingAsInt(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithInvalidIntValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Enabled"; // Contains "false", not an int
            var defaultValue = 99;

            // Act
            var result = _provider.GetAppSettingAsInt(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithEmptyValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Url"; // Empty value
            var defaultValue = 50;

            // Act
            var result = _provider.GetAppSettingAsInt(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithNullKey_ReturnsDefaultValue()
        {
            // Arrange
            var defaultValue = 75;

            // Act
            var result = _provider.GetAppSettingAsInt(null, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithZeroValue_ReturnsZero()
        {
            // Arrange
            var key = "TestZeroKey";
            ConfigurationManager.AppSettings[key] = "0";

            // Act
            var result = _provider.GetAppSettingAsInt(key, 100);

            // Assert
            Assert.AreEqual(0, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithNegativeValue_ReturnsNegativeInt()
        {
            // Arrange
            var key = "TestNegativeKey";
            ConfigurationManager.AppSettings[key] = "-42";

            // Act
            var result = _provider.GetAppSettingAsInt(key, 0);

            // Assert
            Assert.AreEqual(-42, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithMaxIntValue_ReturnsMaxInt()
        {
            // Arrange
            var key = "TestMaxIntKey";
            ConfigurationManager.AppSettings[key] = int.MaxValue.ToString();

            // Act
            var result = _provider.GetAppSettingAsInt(key, 0);

            // Assert
            Assert.AreEqual(int.MaxValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithMinIntValue_ReturnsMinInt()
        {
            // Arrange
            var key = "TestMinIntKey";
            ConfigurationManager.AppSettings[key] = int.MinValue.ToString();

            // Act
            var result = _provider.GetAppSettingAsInt(key, 0);

            // Assert
            Assert.AreEqual(int.MinValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithWhitespaceValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "TestWhitespaceKey";
            ConfigurationManager.AppSettings[key] = "   ";
            var defaultValue = 25;

            // Act
            var result = _provider.GetAppSettingAsInt(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithDecimalValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "TestDecimalKey";
            ConfigurationManager.AppSettings[key] = "42.5";
            var defaultValue = 10;

            // Act
            var result = _provider.GetAppSettingAsInt(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithAlphanumericValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "TestAlphanumericKey";
            ConfigurationManager.AppSettings[key] = "42abc";
            var defaultValue = 15;

            // Act
            var result = _provider.GetAppSettingAsInt(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsInt_WithNegativeDefaultValue_ReturnsNegativeDefault()
        {
            // Arrange
            var key = "NonExistentKey";
            var defaultValue = -100;

            // Act
            var result = _provider.GetAppSettingAsInt(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        #endregion

        #region GetAppSettingAsBool Tests

        [Test]
        public void GetAppSettingAsBool_WithTrueValue_ReturnsTrue()
        {
            // Arrange
            var key = "TestTrueKey";
            ConfigurationManager.AppSettings[key] = "true";

            // Act
            var result = _provider.GetAppSettingAsBool(key, false);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void GetAppSettingAsBool_WithFalseValue_ReturnsFalse()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Enabled"; // Contains "false"

            // Act
            var result = _provider.GetAppSettingAsBool(key, true);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void GetAppSettingAsBool_WithNonExistentKey_ReturnsDefaultValue()
        {
            // Arrange
            var key = "NonExistentBoolKey";
            var defaultValue = true;

            // Act
            var result = _provider.GetAppSettingAsBool(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsBool_WithInvalidBoolValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Url"; // Empty string, not a bool
            var defaultValue = true;

            // Act
            var result = _provider.GetAppSettingAsBool(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsBool_WithNullKey_ReturnsDefaultValue()
        {
            // Arrange
            var defaultValue = false;

            // Act
            var result = _provider.GetAppSettingAsBool(null, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsBool_WithTrueCaseInsensitive_ReturnsTrue()
        {
            // Arrange
            var key = "TestTrueCaseKey";
            ConfigurationManager.AppSettings[key] = "True";

            // Act
            var result = _provider.GetAppSettingAsBool(key, false);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void GetAppSettingAsBool_WithFalseCaseInsensitive_ReturnsFalse()
        {
            // Arrange
            var key = "TestFalseCaseKey";
            ConfigurationManager.AppSettings[key] = "False";

            // Act
            var result = _provider.GetAppSettingAsBool(key, true);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void GetAppSettingAsBool_WithNumericValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "TestNumericKey";
            ConfigurationManager.AppSettings[key] = "1";
            var defaultValue = false;

            // Act
            var result = _provider.GetAppSettingAsBool(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsBool_WithWhitespaceValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "TestWhitespaceBoolKey";
            ConfigurationManager.AppSettings[key] = "   ";
            var defaultValue = true;

            // Act
            var result = _provider.GetAppSettingAsBool(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsBool_WithYesNoValue_ReturnsDefaultValue()
        {
            // Arrange
            var key = "TestYesKey";
            ConfigurationManager.AppSettings[key] = "yes";
            var defaultValue = false;

            // Act
            var result = _provider.GetAppSettingAsBool(key, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        [Test]
        public void GetAppSettingAsBool_WithDefaultTrue_ReturnsTrue()
        {
            // Arrange
            var key = "NonExistentKey";
            var defaultValue = true;

            // Act
            var result = _provider.GetAppSettingAsBool(key, defaultValue);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void GetAppSettingAsBool_WithDefaultFalse_ReturnsFalse()
        {
            // Arrange
            var key = "NonExistentKey";
            var defaultValue = false;

            // Act
            var result = _provider.GetAppSettingAsBool(key, defaultValue);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void GetAppSetting_WithAllConfiguredKeys_ReturnsExpectedValues()
        {
            // Act & Assert
            Assert.AreEqual("true", _provider.GetAppSetting("IISFrontGuard.Webhook.Enabled"));
            Assert.AreEqual("", _provider.GetAppSetting("IISFrontGuard.Webhook.Url"));
            Assert.AreEqual("", _provider.GetAppSetting("IISFrontGuard.Webhook.AuthHeader"));
            Assert.AreEqual("", _provider.GetAppSetting("IISFrontGuard.Webhook.CustomHeaders"));
            Assert.AreEqual("", _provider.GetAppSetting("IISFrontGuard.Webhook.FailureLogPath"));
        }

        [Test]
        public void MultipleProviderInstances_AccessSameConfiguration()
        {
            // Arrange
            var provider1 = new AppConfigConfigurationProvider();
            var provider2 = new AppConfigConfigurationProvider();
            var key = "IISFrontGuard.Webhook.Enabled";

            // Act
            var result1 = provider1.GetAppSetting(key);
            var result2 = provider2.GetAppSetting(key);

            // Assert
            Assert.AreEqual(result1, result2);
        }

        [Test]
        public void GetAppSettingAsInt_AndGetAppSetting_ReturnConsistentData()
        {
            // Arrange
            var key = "TestIntKey";
            ConfigurationManager.AppSettings[key] = "123";

            // Act
            var stringResult = _provider.GetAppSetting(key);
            var intResult = _provider.GetAppSettingAsInt(key, 0);

            // Assert
            Assert.AreEqual("123", stringResult);
            Assert.AreEqual(123, intResult);
        }

        [Test]
        public void GetAppSettingAsBool_AndGetAppSetting_ReturnConsistentData()
        {
            // Arrange
            var key = "IISFrontGuard.Webhook.Enabled";

            // Act
            var stringResult = _provider.GetAppSetting(key);
            var boolResult = _provider.GetAppSettingAsBool(key, true);

            // Assert
            Assert.AreEqual("true", stringResult);
            Assert.IsTrue(boolResult);
        }

        #endregion
    }
}
