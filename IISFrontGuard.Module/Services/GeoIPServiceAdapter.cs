using IISFrontGuard.Module.Abstractions;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Adapter for GeoIP lookup services using the MaxMind GeoIP2 database.
    /// </summary>
    public class GeoIPServiceAdapter : IGeoIPService
    {
        private readonly string _databasePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeoIPServiceAdapter"/> class.
        /// </summary>
        /// <param name="databasePath">The file path to the MaxMind GeoIP2 database file.</param>
        public GeoIPServiceAdapter(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>
        /// Retrieves geographic information for an IP address using the GeoIP2 database.
        /// </summary>
        /// <param name="ipAddress">The IP address to look up.</param>
        /// <returns>A CountryResponse containing geographic information, or an empty response if lookup fails.</returns>
        public CountryResponse GetGeoInfo(string ipAddress)
        {
            CountryResponse result = new CountryResponse();
            try
            {
                using (var reader = new DatabaseReader(_databasePath))
                {
                    result = reader.Country(ipAddress);
                }   
            }
            catch
            {
                // Silently fail - geo resolution is non-critical, return empty response
            }

            return result;
        }
    }
}
