using IISFrontGuard.Module.Abstractions;

namespace IISFrontGuard.Module.IntegrationTests.Services
{
    public class TestConfigurationProvider : IConfigurationProvider
    {
        public string GetAppSetting(string key)
        {
            return System.Configuration.ConfigurationManager.AppSettings[key];
        }

        public bool GetAppSettingAsBool(string key, bool defaultValue)
        {
            var value = GetAppSetting(key);
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        public int GetAppSettingAsInt(string key, int defaultValue)
        {
            var value = GetAppSetting(key);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        public string GetConnectionString(string name)
        {
            return System.Configuration.ConfigurationManager.ConnectionStrings[name]?.ConnectionString;
        }
    }
}
