using IISFrontGuard.Module.Abstractions;
using System.Configuration;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Provides configuration access using the standard .NET ConfigurationManager.
    /// </summary>
    public class AppConfigConfigurationProvider : IConfigurationProvider
    {
        /// <summary>
        /// Retrieves an application setting value from app.config/web.config.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or null if not found.</returns>
        public string GetAppSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        /// <summary>
        /// Retrieves a connection string from app.config/web.config.
        /// </summary>
        /// <param name="name">The connection string name.</param>
        /// <returns>The connection string, or null if not found.</returns>
        public string GetConnectionString(string name)
        {
            return ConfigurationManager.ConnectionStrings[name]?.ConnectionString;
        }

        /// <summary>
        /// Retrieves an application setting as an integer.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="defaultValue">The default value if the setting is not found or cannot be parsed.</param>
        /// <returns>The setting value as an integer.</returns>
        public int GetAppSettingAsInt(string key, int defaultValue)
        {
            var value = GetAppSetting(key);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Retrieves an application setting as a boolean.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="defaultValue">The default value if the setting is not found or cannot be parsed.</param>
        /// <returns>The setting value as a boolean.</returns>
        public bool GetAppSettingAsBool(string key, bool defaultValue)
        {
            var value = GetAppSetting(key);
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}
