using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DebugExport;
#nullable enable

/// <summary>
/// Makes the replacement for each scrubbed value, and gives one original the same replacement everywhere in an export
/// </summary>
public sealed partial class ExportJunk
{
    private const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int AddressesPerRange = 254;
    // RFC 5737 documentation ranges, then RFC 2544's 198.18.0.0/15 benchmark range once their 762 addresses run out
    private static readonly string[] DocumentationRanges = ["192.0.2.", "198.51.100.", "203.0.113."];
    private static readonly HashSet<ExportScrubKind> CaseInsensitiveKinds = [ExportScrubKind.Email, ExportScrubKind.Host, ExportScrubKind.Username];

    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(32);
    private readonly Dictionary<string, string> _replaced = [];
    private readonly Dictionary<ExportScrubKind, Dictionary<string, string>> _byKind = [];
    private int _ipv4Count;
    private int _ipv6Count;
    private int _hostCount;
    private int _emailCount;
    private int _usernameCount;

    /// <summary>
    /// Every original replaced so far, mapped to its replacement. Free text and fingerprints are left out
    /// </summary>
    public IReadOnlyDictionary<string, string> Replaced => _replaced;

    /// <param name="owner">The row the value sits on, as <c>user-3</c> or <c>device-7</c>. Names an email, and is the username stand-in.
    /// Without one, an email becomes <c>email-n</c> and a username <c>username-n</c></param>
    public string? Replace(ExportScrubKind kind, string? value, string? owner = null)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var junk = kind switch
        {
            ExportScrubKind.FreeText => FreeText(value),
            ExportScrubKind.Fingerprint => Convert.ToHexStringLower(HMACSHA256.HashData(_salt, Encoding.UTF8.GetBytes(value))),
            ExportScrubKind.IpAddress => IpAddressList(value),
            ExportScrubKind.Host => Host(value),
            ExportScrubKind.Email => MatchCase(value, Remember(kind, value, () => $"{owner ?? $"email-{++_emailCount}"}@scrubbed.invalid")),
            ExportScrubKind.Username => MatchCase(value, Remember(kind, value, () => owner ?? $"username-{++_usernameCount}")),
            ExportScrubKind.Random => Remember(kind, value, () => RandomText(value.Length)),
            ExportScrubKind.Guid => Remember(kind, value, () => NewGuidLike(value)),
            ExportScrubKind.Phone => Remember(kind, value, () => new string(value.Select(c => char.IsDigit(c) ? (char)('0' + RandomNumberGenerator.GetInt32(10)) : c).ToArray())),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a value replacement"),
        };

        Record(kind, value, junk);
        return junk;
    }

    private void Record(ExportScrubKind kind, string original, string junk)
    {
        if (kind is ExportScrubKind.FreeText or ExportScrubKind.Fingerprint || original == junk) return;

        _replaced.TryAdd(original, junk);
    }

    private string Remember(ExportScrubKind kind, string original, Func<string> make)
    {
        if (!_byKind.TryGetValue(kind, out var map))
        {
            map = new Dictionary<string, string>(CaseInsensitiveKinds.Contains(kind) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            _byKind[kind] = map;
        }

        if (!map.TryGetValue(original, out var junk))
        {
            junk = make();
            map[original] = junk;
        }

        return junk;
    }

    /// <summary>
    /// A comma list such as the <c>IpAddresses</c> setting has each address replaced, spacing kept
    /// </summary>
    private string IpAddressList(string value)
    {
        return string.Join(',', value.Split(',').Select(part =>
        {
            var address = part.Trim();
            return address.Length == 0 ? part : part.Replace(address, IpAddress(address));
        }));
    }

    private string IpAddress(string value)
    {
        if (!IPAddress.TryParse(value, out var address)) return Remember(ExportScrubKind.IpAddress, value, () => RandomText(value.Length));
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return value;

        var junk = Remember(ExportScrubKind.IpAddress, value, address.AddressFamily == AddressFamily.InterNetworkV6 ? NextIpv6 : NextIpv4);
        Record(ExportScrubKind.IpAddress, value, junk);

        return junk;
    }

    private string NextIpv4()
    {
        var n = _ipv4Count++;
        if (n < DocumentationRanges.Length * AddressesPerRange)
        {
            return $"{DocumentationRanges[n / AddressesPerRange]}{n % AddressesPerRange + 1}";
        }

        var rest = (n - DocumentationRanges.Length * AddressesPerRange) / AddressesPerRange;
        var host = (n - DocumentationRanges.Length * AddressesPerRange) % AddressesPerRange + 1;
        if (rest >= 2 * 256) throw new InvalidOperationException("More distinct IPv4 addresses than the reserved ranges hold");

        return $"198.{18 + rest / 256}.{rest % 256}.{host}";
    }

    private string NextIpv6() => $"2001:db8::{++_ipv6Count:x}";

    /// <summary>
    /// The host in a URL, <c>host:port</c> or bare host. Scheme, port and path are kept, credentials replaced
    /// </summary>
    private string Host(string value)
    {
        var match = HostRegex().Match(value);
        if (!match.Success) return Remember(ExportScrubKind.Host, value, () => RandomText(value.Length));

        var host = match.Groups["host"].Value;
        string junkHost;
        if (host.StartsWith('[') && host.EndsWith(']'))
        {
            junkHost = $"[{IpAddress(host[1..^1])}]";
        }
        else if (IPAddress.TryParse(host, out _))
        {
            junkHost = IpAddress(host);
        }
        else if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            junkHost = host;
        }
        else
        {
            junkHost = MatchCase(host, Remember(ExportScrubKind.Host, host, () => $"host-{++_hostCount}.invalid"));
            Record(ExportScrubKind.Host, host, junkHost);
        }

        var userInfo = match.Groups["userinfo"].Value;
        if (userInfo.Length > 0)
        {
            userInfo = string.Join(':', userInfo[..^1].Split(':').Select(p => p.Length == 0 ? p : Remember(ExportScrubKind.Random, p, () => RandomText(p.Length)))) + "@";
        }

        return $"{match.Groups["scheme"].Value}{userInfo}{junkHost}{match.Groups["rest"].Value}";
    }

    private static string FreeText(string value) => FreeTextRegex().Replace(value, m =>
        m.Groups["markup"].Success ? m.Value : m.Groups["letter"].Success ? "x" : "0");

    private static string NewGuidLike(string value)
    {
        if (!Guid.TryParse(value, out _)) return RandomText(value.Length);

        var format = value.Length switch
        {
            32 => "N",
            38 => "B",
            _ => "D",
        };
        var guid = Guid.NewGuid().ToString(format);
        return value.Any(char.IsUpper) ? guid.ToUpperInvariant() : guid;
    }

    private static string RandomText(int length) => RandomNumberGenerator.GetString(Alphanumeric, length);

    /// <summary>
    /// A <c>Normalized*</c> column holds the upper-case original, so it gets the upper-case replacement
    /// </summary>
    private static string MatchCase(string original, string junk) =>
        original.Any(char.IsLetter) && original == original.ToUpperInvariant() ? junk.ToUpperInvariant() : junk;

    /// <example>
    /// <c>smtp://joe:pw@mail.example.com:587/path</c> splits into scheme <c>smtp://</c>, userinfo <c>joe:pw@</c>,
    /// host <c>mail.example.com</c> and rest <c>:587/path</c>. A bare <c>mail.example.com</c> is host only
    /// </example>
    [GeneratedRegex(@"^(?<scheme>[A-Za-z][A-Za-z0-9+.\-]*://)?(?<userinfo>[^@/?#\s]*@)?(?<host>\[[^\]]*\]|[^:/?#\s]+)(?<rest>.*)$", RegexOptions.Singleline)]
    private static partial Regex HostRegex();

    /// <example>
    /// In <c>&lt;p&gt;Great &amp;amp; 10/10&lt;/p&gt;</c> the tags and <c>&amp;amp;</c> match as markup, each letter as a letter and
    /// each digit as a digit, giving <c>&lt;p&gt;xxxxx &amp;amp; 00/00&lt;/p&gt;</c>
    /// </example>
    [GeneratedRegex(@"(?<markup><[^>]*>|&#?[A-Za-z0-9]+;)|(?<letter>\p{L})|(?<digit>\p{Nd})")]
    private static partial Regex FreeTextRegex();
}
