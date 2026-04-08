using System.Net;
using Kavita.Common.Helpers;

namespace Kavita.Services.Tests.Helpers;

public class IpBlocklistTests
{
    #region IPv4 Blocked Ranges

    [Theory]
    [InlineData("127.0.0.1")] // loopback
    [InlineData("127.255.255.255")] // loopback end
    public void IsBlockedAddress_Loopback_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("0.0.0.0")] // this network start
    [InlineData("0.255.255.255")] // this network end
    public void IsBlockedAddress_ThisNetwork_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("10.0.0.0")] // RFC1918 start
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")] // RFC1918 end
    public void IsBlockedAddress_Private10_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("172.16.0.0")] // RFC1918 start
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")] // RFC1918 end
    public void IsBlockedAddress_Private172_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("192.168.0.0")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.255.255")]
    public void IsBlockedAddress_Private192_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("100.64.0.0")] // CGNAT start
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")] // CGNAT end
    public void IsBlockedAddress_Cgnat_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("169.254.0.0")] // link-local start
    [InlineData("169.254.169.254")] // AWS metadata
    [InlineData("169.254.255.255")] // link-local end
    public void IsBlockedAddress_LinkLocal_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("192.0.0.0")] // IETF protocol assignments
    [InlineData("192.0.0.255")]
    public void IsBlockedAddress_IetfProtocol_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("192.0.2.0")] // TEST-NET-1
    [InlineData("192.0.2.255")]
    public void IsBlockedAddress_TestNet1_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("198.51.100.0")] // TEST-NET-2
    [InlineData("198.51.100.255")]
    public void IsBlockedAddress_TestNet2_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("203.0.113.0")] // TEST-NET-3
    [InlineData("203.0.113.255")]
    public void IsBlockedAddress_TestNet3_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("198.18.0.0")] // benchmarking start
    [InlineData("198.19.255.255")] // benchmarking end
    public void IsBlockedAddress_Benchmarking_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("224.0.0.0")] // multicast start
    [InlineData("239.255.255.255")] // multicast end
    public void IsBlockedAddress_Multicast_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("240.0.0.0")] // reserved start
    [InlineData("254.255.255.255")] // reserved end
    public void IsBlockedAddress_Reserved_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Fact]
    public void IsBlockedAddress_Broadcast_ReturnsTrue()
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse("255.255.255.255")));
    }

    #endregion

    #region IPv6 Blocked Ranges

    [Fact]
    public void IsBlockedAddress_IPv6Loopback_ReturnsTrue()
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.IPv6Loopback)); // ::1
    }

    [Fact]
    public void IsBlockedAddress_IPv6Unspecified_ReturnsTrue()
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse("::")));
    }

    [Theory]
    [InlineData("fe80::1")] // link-local
    [InlineData("fe80::abcd:1234")]
    public void IsBlockedAddress_IPv6LinkLocal_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("fc00::1")] // ULA
    [InlineData("fd00::1")]
    [InlineData("fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
    public void IsBlockedAddress_IPv6Ula_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("ff00::1")] // multicast
    [InlineData("ff02::1")]
    public void IsBlockedAddress_IPv6Multicast_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("2001:db8::1")] // documentation
    [InlineData("2001:db8:ffff:ffff:ffff:ffff:ffff:ffff")]
    public void IsBlockedAddress_IPv6Documentation_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("100::1")] // discard prefix
    [InlineData("100::ffff:ffff:ffff:ffff")]
    public void IsBlockedAddress_IPv6Discard_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    #endregion

    #region IPv4-Mapped IPv6

    [Theory]
    [InlineData("::ffff:127.0.0.1")] // mapped loopback
    [InlineData("::ffff:10.0.0.1")] // mapped private
    [InlineData("::ffff:192.168.1.1")] // mapped private
    [InlineData("::ffff:169.254.169.254")] // mapped link-local (AWS metadata)
    public void IsBlockedAddress_IPv4MappedIPv6_ReturnsTrue(string ip)
    {
        Assert.True(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    #endregion

    #region Allowed Public IPs

    [Theory]
    [InlineData("8.8.8.8")] // Google DNS
    [InlineData("1.1.1.1")] // Cloudflare DNS
    [InlineData("104.16.0.1")] // Cloudflare
    [InlineData("151.101.1.140")] // Reddit
    [InlineData("172.15.255.255")] // just below 172.16/12
    [InlineData("172.32.0.0")] // just above 172.31/12
    [InlineData("100.63.255.255")] // just below CGNAT
    [InlineData("100.128.0.0")] // just above CGNAT
    [InlineData("192.1.0.1")] // not in 192.0.0/24 or 192.168/16
    public void IsBlockedAddress_PublicIPv4_ReturnsFalse(string ip)
    {
        Assert.False(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("2606:4700::1111")] // Cloudflare
    [InlineData("2001:4860:4860::8888")] // Google DNS
    public void IsBlockedAddress_PublicIPv6_ReturnsFalse(string ip)
    {
        Assert.False(IpBlocklist.IsBlockedAddress(IPAddress.Parse(ip)));
    }

    [Fact]
    public void IsBlockedAddress_IPv4MappedPublic_ReturnsFalse()
    {
        Assert.False(IpBlocklist.IsBlockedAddress(IPAddress.Parse("::ffff:8.8.8.8")));
    }

    #endregion
}
