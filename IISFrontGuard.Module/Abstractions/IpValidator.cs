using LukeSkywalker.IPNetwork;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace IISFrontGuard.Module.Abstractions
{
    /// <summary>
    /// Validates whether IP addresses fall within specified CIDR ranges.
    /// </summary>
    public class IpValidator
    {
        private readonly IPNetwork[] _networks;

        /// <summary>
        /// Initializes a new instance of the <see cref="IpValidator"/> class.
        /// </summary>
        /// <param name="cidrList">A collection of CIDR notation strings (e.g., "192.168.1.0/24").</param>
        public IpValidator(IEnumerable<string> cidrList)
            => _networks = cidrList.Select(IPNetwork.Parse).ToArray();

        /// <summary>
        /// Determines whether an IP address string is within any of the configured IP ranges.
        /// </summary>
        /// <param name="ipString">The IP address as a string.</param>
        /// <returns>True if the IP is within any configured range; otherwise, false.</returns>
        public bool IsInIp(string ipString)
        {
            if (!IPAddress.TryParse(ipString, out var ip))
                return false;

            return IsInIp(ip);
        }

        /// <summary>
        /// Determines whether an IP address is within any of the configured IP ranges.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>True if the IP is within any configured range; otherwise, false.</returns>
        public bool IsInIp(IPAddress ip)
        {
            for (int i = 0; i < _networks.Length; i++)
            {
                // Skip comparison if address families don't match (e.g., IPv4 network vs IPv6 address)
                if (_networks[i].Network.AddressFamily != ip.AddressFamily)
                    continue;

                if (IPNetwork.Contains(_networks[i], ip))
                    return true;
            }

            return false;
        }
    }
}