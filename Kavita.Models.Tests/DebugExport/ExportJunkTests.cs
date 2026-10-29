using System.Text.RegularExpressions;
using Kavita.Models.DebugExport;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Tests.DebugExport;

public class ExportJunkTests
{
    [Theory]
    [InlineData(ExportScrubKind.IpAddress, "82.123.6.194", "192.0.2.1")]
    [InlineData(ExportScrubKind.IpAddress, "fe80::1c2b:3a4f", "2001:db8::1")]
    [InlineData(ExportScrubKind.IpAddress, "10.0.0.5, 82.123.6.194", "192.0.2.1, 192.0.2.2")]
    [InlineData(ExportScrubKind.IpAddress, "127.0.0.1", "127.0.0.1")]
    [InlineData(ExportScrubKind.IpAddress, "::1", "::1")]
    [InlineData(ExportScrubKind.IpAddress, "0.0.0.0", "0.0.0.0")]
    [InlineData(ExportScrubKind.IpAddress, "::", "::")]
    [InlineData(ExportScrubKind.Host, "smtp.gmail.com", "host-1.invalid")]
    [InlineData(ExportScrubKind.Host, "https://auth.example.com:8443/realms/kavita", "https://host-1.invalid:8443/realms/kavita")]
    [InlineData(ExportScrubKind.Host, "http://192.168.1.5:5000/api", "http://192.0.2.1:5000/api")]
    [InlineData(ExportScrubKind.Host, "http://localhost:5000", "http://localhost:5000")]
    [InlineData(ExportScrubKind.FreeText, "<p>Great &amp; <b>twist</b> in 2</p>", "<p>xxxxx &amp; <b>xxxxx</b> xx 0</p>")]
    public void ReplacesByKind(ExportScrubKind kind, string original, string expected)
    {
        Assert.Equal(expected, new ExportJunk().Replace(kind, original));
    }

    [Theory]
    [InlineData(ExportScrubKind.Random, "abc123XYZ", "^[A-Za-z0-9]{9}$")]
    [InlineData(ExportScrubKind.Guid, "3f2504e0-4f89-11d3-9a0c-0305e82c3301", "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    [InlineData(ExportScrubKind.Guid, "3F2504E04F8911D39A0C0305E82C3301", "^[0-9A-F]{32}$")]
    [InlineData(ExportScrubKind.Guid, "not-a-guid-sub", "^[A-Za-z0-9]{14}$")]
    [InlineData(ExportScrubKind.Phone, "+1 (555) 123-4567", @"^\+\d \(\d{3}\) \d{3}-\d{4}$")]
    [InlineData(ExportScrubKind.Fingerprint, "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08", "^[0-9a-f]{64}$")]
    public void KeepsTheShape(ExportScrubKind kind, string original, string pattern)
    {
        var junk = new ExportJunk().Replace(kind, original)!;

        Assert.NotEqual(original, junk);
        Assert.Matches(new Regex(pattern), junk);
    }

    [Fact]
    public void EmailAndUsernameAreNamedByTheirOwner()
    {
        var junk = new ExportJunk();

        Assert.Equal("user-3@scrubbed.invalid", junk.Replace(ExportScrubKind.Email, "joe@example.com", "user-3"));
        Assert.Equal("user-3", junk.Replace(ExportScrubKind.Username, "joe", "user-3"));
    }

    [Fact]
    public void OneOriginalGetsOneReplacement()
    {
        var junk = new ExportJunk();
        var token = junk.Replace(ExportScrubKind.Random, "token-0123456789");

        Assert.Equal(token, junk.Replace(ExportScrubKind.Random, "token-0123456789"));
        Assert.Equal("user-3@scrubbed.invalid", junk.Replace(ExportScrubKind.Email, "joe@example.com", "user-3"));
        Assert.Equal("user-3@scrubbed.invalid", junk.Replace(ExportScrubKind.Email, "JOE@EXAMPLE.COM", "user-9"), ignoreCase: true);
        Assert.Equal("host-1.invalid", junk.Replace(ExportScrubKind.Host, "smtp.gmail.com"));
        Assert.Equal("https://host-1.invalid/", junk.Replace(ExportScrubKind.Host, "https://SMTP.gmail.com/"));
        Assert.Equal(token, junk.Replaced["token-0123456789"]);
    }

    [Fact]
    public void Ipv4RunsPastTheDocumentationRanges()
    {
        var junk = new ExportJunk();
        var replaced = Enumerable.Range(0, 763).Select(i => junk.Replace(ExportScrubKind.IpAddress, $"10.{i / 256}.{i % 256}.1")).ToList();

        Assert.Equal("198.51.100.1", replaced[254]);
        Assert.Equal("203.0.113.1", replaced[508]);
        Assert.Equal("198.18.0.1", replaced[762]);
        Assert.Equal(replaced.Count, replaced.Distinct().Count());
    }

    [Fact]
    public void FingerprintsStayEqualWithinAnExport()
    {
        var first = new ExportJunk();
        var second = new ExportJunk();

        Assert.Equal(first.Replace(ExportScrubKind.Fingerprint, "abc"), first.Replace(ExportScrubKind.Fingerprint, "abc"));
        Assert.NotEqual(first.Replace(ExportScrubKind.Fingerprint, "abc"), second.Replace(ExportScrubKind.Fingerprint, "abc"));
    }

    [Fact]
    public void FreeTextIsNotHandedToTheLogs()
    {
        var junk = new ExportJunk();
        junk.Replace(ExportScrubKind.FreeText, "Kindle");

        Assert.DoesNotContain("Kindle", junk.Replaced.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullAndEmptyStayAsTheyAre(string? original)
    {
        Assert.Equal(original, new ExportJunk().Replace(ExportScrubKind.Random, original));
    }
}
