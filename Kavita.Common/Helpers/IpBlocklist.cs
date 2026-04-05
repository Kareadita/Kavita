using System.Net;
using System.Net.Sockets;

namespace Kavita.Common.Helpers;

/// <summary>
/// Centralized IP blocklist for SSRF protection. Used by both UrlValidationService (pre-flight)
/// and FlurlConfiguration's ConnectCallback (connection-time).
/// </summary>
public static class IpBlocklist
{
    /// <summary>
    /// Returns true if the given IP address is non-public (loopback, private, reserved, etc)
    /// and should be blocked for outbound requests to user-provided URLs.
    /// Handles IPv4-mapped IPv6 addresses by unwrapping before checking.
    /// </summary>
    public static bool IsBlockedAddress(IPAddress address)
    {
        var ipToCheck = address;

        // Unwrap IPv6-mapped IPv4 addresses (e.g. ::ffff:192.168.1.1)
        if (ipToCheck.IsIPv4MappedToIPv6)
        {
            ipToCheck = ipToCheck.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ipToCheck)) return true;

        if (ipToCheck.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ipToCheck.GetAddressBytes();

            // 0.0.0.0/8 — "this network", can alias to loopback
            if (bytes[0] == 0) return true;

            // 10.0.0.0/8 — private
            if (bytes[0] == 10) return true;

            // 100.64.0.0/10 — CGNAT / shared address space
            if (bytes[0] == 100 && (bytes[1] & 0xC0) == 64) return true;

            // 172.16.0.0/12 — private
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

            // 192.0.0.0/24 — IETF protocol assignments
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) return true;

            // 192.0.2.0/24 — TEST-NET-1 (documentation)
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) return true;

            // 192.168.0.0/16 — private
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            // 198.18.0.0/15 — benchmarking
            if (bytes[0] == 198 && (bytes[1] & 0xFE) == 18) return true;

            // 198.51.100.0/24 — TEST-NET-2 (documentation)
            if (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) return true;

            // 203.0.113.0/24 — TEST-NET-3 (documentation)
            if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) return true;

            // 169.254.0.0/16 — link-local
            if (bytes[0] == 169 && bytes[1] == 254) return true;

            // 224.0.0.0/4 — multicast
            if ((bytes[0] & 0xF0) == 224) return true;

            // 240.0.0.0/4 — reserved / future use
            if ((bytes[0] & 0xF0) == 240) return true;

            // 255.255.255.255 — broadcast
            if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255) return true;
        }
        else if (ipToCheck.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // :: — unspecified address
            if (ipToCheck.Equals(IPAddress.IPv6None)) return true;

            // fe80::/10 — link-local
            if (ipToCheck.IsIPv6LinkLocal) return true;

            // fc00::/7 — Unique Local Address (private)
            if (IsInIPv6Prefix(ipToCheck, [0xFC, 0x00], 7)) return true;

            // ff00::/8 — multicast
            if (IsInIPv6Prefix(ipToCheck, [0xFF], 8)) return true;

            // 2001:db8::/32 — documentation
            if (IsInIPv6Prefix(ipToCheck, [0x20, 0x01, 0x0D, 0xB8], 32)) return true;

            // 100::/64 — discard prefix
            if (IsInIPv6Prefix(ipToCheck, [0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], 64)) return true;
        }

        return false;
    }

    private static bool IsInIPv6Prefix(IPAddress address, byte[] prefix, int prefixLengthBits)
    {
        var bytes = address.GetAddressBytes();
        var fullBytes = prefixLengthBits / 8;

        for (var i = 0; i < fullBytes && i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i]) return false;
        }

        var remainingBits = prefixLengthBits % 8;
        if (remainingBits > 0 && fullBytes < prefix.Length)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((bytes[fullBytes] & mask) != (prefix[fullBytes] & mask)) return false;
        }

        return true;
    }
}
