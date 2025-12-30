namespace IISFrontGuard.Module.Abstractions
{
    /// <summary>
    /// Defines an abstraction for configuration access to enable testability.
    /// </summary>
    public interface IConfigurationProvider
    {
        /// <summary>
        /// Retrieves an application setting value.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or null if not found.</returns>
        string GetAppSetting(string key);

        /// <summary>
        /// Retrieves a connection string.
        /// </summary>
        /// <param name="name">The connection string name.</param>
        /// <returns>The connection string, or null if not found.</returns>
        string GetConnectionString(string name);

        /// <summary>
        /// Retrieves an application setting as an integer.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="defaultValue">The default value if the setting is not found or cannot be parsed.</param>
        /// <returns>The setting value as an integer.</returns>
        int GetAppSettingAsInt(string key, int defaultValue);

        /// <summary>
        /// Retrieves an application setting as a boolean.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="defaultValue">The default value if the setting is not found or cannot be parsed.</param>
        /// <returns>The setting value as a boolean.</returns>
        bool GetAppSettingAsBool(string key, bool defaultValue);
    }
}
