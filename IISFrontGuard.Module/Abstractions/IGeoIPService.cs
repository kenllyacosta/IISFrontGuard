using MaxMind.GeoIP2.Responses;

namespace IISFrontGuard.Module.Abstractions
{
    /// <summary>
    /// Defines an abstraction for GeoIP lookup services.
    /// </summary>
    public interface IGeoIPService
    {
        /// <summary>
        /// Retrieves geographic information for an IP address.
        /// </summary>
        /// <param name="ipAddress">The IP address to look up.</param>
        /// <returns>A CountryResponse containing geographic information.</returns>
        CountryResponse GetGeoInfo(string ipAddress);
    }
}
