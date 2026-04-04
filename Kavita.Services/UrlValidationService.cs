using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common;

namespace Kavita.Services;

public class UrlValidationService : IUrlValidationService
{
    public async Task ValidateUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new KavitaException("URL is malformed");
        }

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new KavitaException("Only HTTPS URLs are allowed");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host);
        }
        catch (SocketException)
        {
            throw new KavitaException("Unable to resolve hostname");
        }

        if (addresses.Length == 0)
        {
            throw new KavitaException("Unable to resolve hostname");
        }

        foreach (var address in addresses)
        {
            var ipToCheck = address;

            // Unwrap IPv6-mapped IPv4 addresses
            if (ipToCheck.IsIPv4MappedToIPv6)
            {
                ipToCheck = ipToCheck.MapToIPv4();
            }

            if (IsBlockedAddress(ipToCheck))
            {
                throw new KavitaException("URL resolves to a blocked address");
            }
        }
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            // 10.0.0.0/8
            if (bytes[0] == 10) return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
        }
        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // fe80::/10 (link-local)
            if (address.IsIPv6LinkLocal) return true;
        }

        return false;
    }
}
