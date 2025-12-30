using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using System.Web;

namespace IISFrontGuard.Module.Services
{
    /// <summary>
    /// Provides static methods for GeoIP lookup using the MaxMind GeoIP2 database.
    /// </summary>
    public static class GeoIPService
    {
        /// <summary>
        /// Retrieves geographic information for an IP address.
        /// </summary>
        /// <param name="ipAddress">The IP address to look up.</param>
        /// <param name="path">Optional path to the GeoIP2 database file. If empty, uses default location.</param>
        /// <returns>A CountryResponse containing geographic information, or an empty response if lookup fails.</returns>
        public static CountryResponse GetGeoInfo(string ipAddress, string path = "")
        {
            path = string.IsNullOrWhiteSpace(path)
                ? HttpContext.Current?.Server.MapPath("~/App_Data/GeoIP2-Country.mmdb")
                : path;

            CountryResponse result = new CountryResponse();
            try
            {
                using (var reader = new DatabaseReader(path))
                    result = reader.Country(ipAddress);
            }
            catch
            {
                // Silently fail - geo resolution is non-critical, return empty response
            }

            return result;
        }
    }
}