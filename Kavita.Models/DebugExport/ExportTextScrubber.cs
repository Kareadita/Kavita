using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DebugExport;
#nullable enable

/// <summary>
/// Scrubs free text, such as log lines and error messages, with the replacements the database scrub already made.
/// Build it after the database scrub, so it knows every value that scrub replaced
/// </summary>
public sealed partial class ExportTextScrubber
{
    private const int ShortValueLength = 5;

    private readonly ExportJunk _junk;
    private readonly IReadOnlyDictionary<string, string> _known;
    private readonly Dictionary<string, string> _usernames;
    private readonly Regex? _knownRegex;
    private readonly Regex? _usernameRegex;

    /// <param name="usernames">Original username to its replacement, as the database scrub returned it</param>
    public ExportTextScrubber(ExportJunk junk, IReadOnlyDictionary<string, string> usernames)
    {
        _junk = junk;
        _usernames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (original, replaced) in usernames)
        {
            _usernames.TryAdd(original, replaced);
        }

        _known = junk.Replaced
            .Where(pair => !_usernames.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        _knownRegex = Alternation(_known.Keys, wholeWordUpTo: ShortValueLength, RegexOptions.None);
        _usernameRegex = Alternation(_usernames.Keys, wholeWordUpTo: int.MaxValue, RegexOptions.IgnoreCase);
    }

    public string Scrub(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;

        line = ReplaceKnownValues(line);
        line = ReplaceUsernames(line);
        line = ReplaceEmailsAndIps(line);

        return ReplaceSecretShapedValues(line);
    }

    /// <summary>
    /// Every value the database scrub replaced gets the same replacement here, inside paths too
    /// </summary>
    private string ReplaceKnownValues(string line) =>
        _knownRegex == null ? line : _knownRegex.Replace(line, m => _known[m.Value]);

    private string ReplaceUsernames(string line)
    {
        if (_usernameRegex == null) return line;

        // A word holding a path separator is a path, and the username in /home/joe is a folder name worth keeping
        return NonSpaceRegex().Replace(line, word =>
            word.Value.Contains('/') || word.Value.Contains('\\') ? word.Value : _usernameRegex.Replace(word.Value, m => _usernames[m.Value]));
    }

    /// <summary>
    /// Emails and IPs the database never held. Each gets its junk through <see cref="ExportJunk"/>, so later lines and other files agree
    /// </summary>
    private string ReplaceEmailsAndIps(string line)
    {
        // The known-value replacement already wrote junk into the line, and junk only uses .invalid and reserved ranges, so those are skipped
        line = EmailRegex().Replace(line, m =>
            m.Value.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) ? m.Value : _junk.Replace(ExportScrubKind.Email, m.Value)!);
        line = Ipv4Regex().Replace(line, m => KeepIp(m.Value) ? m.Value : _junk.Replace(ExportScrubKind.IpAddress, m.Value)!);

        return Ipv6Regex().Replace(line, m => KeepIp(m.Value) ? m.Value : _junk.Replace(ExportScrubKind.IpAddress, m.Value)!);
    }

    /// <summary>
    /// Tokens and keys in URL parameters, and JWTs, even when the database never held them: a token refreshed since the line was written
    /// </summary>
    private string ReplaceSecretShapedValues(string line)
    {
        line = SecretParameterRegex().Replace(line, m => _junk.Replace(ExportScrubKind.Random, m.Value)!);

        return JwtRegex().Replace(line, m => _junk.Replace(ExportScrubKind.Random, m.Value)!);
    }

    /// <summary>
    /// Kept: anything that doesn't parse, 0.0.0.0/8 (Kavita's version reads as one: <c>v0.9.1.16</c>), loopback, and the
    /// documentation and benchmark ranges <see cref="ExportJunk"/> hands out
    /// </summary>
    private static bool KeepIp(string value)
    {
        if (!IPAddress.TryParse(value, out var address)) return true;
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6Any)) return true;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8;
        }

        return bytes[0] == 0
               || bytes is [192, 0, 2, _] or [198, 51, 100, _] or [203, 0, 113, _]
               || bytes[0] == 198 && bytes[1] is 18 or 19;
    }

    /// <summary>
    /// One regex matching any of <paramref name="values"/>, longest first so a value that contains another wins.
    /// Values up to <paramref name="wholeWordUpTo"/> characters only match as a whole word
    /// </summary>
    /// <example>
    /// <c>["al", "a1b2c3d4e5f6"]</c> with <c>wholeWordUpTo: 5</c> builds <c>a1b2c3d4e5f6|(?&lt;![\p{L}\p{N}_])al(?![\p{L}\p{N}_])</c>,
    /// which matches <c>al</c> in <c>[al] read</c> but not in <c>also</c>
    /// </example>
    private static Regex? Alternation(IEnumerable<string> values, int wholeWordUpTo, RegexOptions options)
    {
        const string notAfterWordCharacter = @"(?<![\p{L}\p{N}_])";
        const string notBeforeWordCharacter = @"(?![\p{L}\p{N}_])";

        var parts = values
            .Where(v => v.Length > 0)
            .OrderByDescending(v => v.Length)
            .Select(v => v.Length <= wholeWordUpTo ? notAfterWordCharacter + Regex.Escape(v) + notBeforeWordCharacter : Regex.Escape(v))
            .ToList();

        return parts.Count == 0 ? null : new Regex(string.Join('|', parts), options | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    /// <example><c>[joe] started scan of /home/joe/manga</c> matches <c>[joe]</c>, <c>started</c>, ..., <c>/home/joe/manga</c></example>
    [GeneratedRegex(@"\S+")]
    private static partial Regex NonSpaceRegex();

    /// <example>Matches <c>bob@x.com</c> in <c>550 bob@x.com: mailbox unavailable</c>, without the trailing colon</example>
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    /// <example>
    /// Matches <c>82.123.6.194</c> in <c>login from 82.123.6.194.</c>. Not part of a longer dotted number: <c>1.2.3.4.5</c> doesn't match.
    /// <c>0.9.1.16</c> in <c>v0.9.1.16</c> does match, and <see cref="KeepIp"/> keeps it
    /// </example>
    [GeneratedRegex(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?!\.?\d)")]
    private static partial Regex Ipv4Regex();

    /// <example>
    /// Matches <c>2001:db8::1</c>, <c>fe80::1c2b:3d4e</c> and <c>fd00:1:2:3:4:5:6:7</c>. A clock time like <c>12:34:56</c> doesn't
    /// match, since an address needs <c>::</c> or eight groups
    /// </example>
    [GeneratedRegex(@"(?<![0-9A-Fa-f:])(?:(?:[0-9A-Fa-f]{1,4}:){7}[0-9A-Fa-f]{1,4}|(?:[0-9A-Fa-f]{1,4}:)*[0-9A-Fa-f]{0,4}::(?:[0-9A-Fa-f]{1,4}:)*[0-9A-Fa-f]{0,4})(?![0-9A-Fa-f:])")]
    private static partial Regex Ipv6Regex();

    /// <example>In <c>/api/want-to-read?provider=AniList&amp;accessToken=eyJ0eX&amp;page=2</c> matches only <c>eyJ0eX</c>, the value</example>
    [GeneratedRegex(@"(?<=[?&](?:accessToken|apiKey|api_key|token)=)[^&\s""'#]+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretParameterRegex();

    /// <example>Matches <c>eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl</c>: header, payload and signature</example>
    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]*")]
    private static partial Regex JwtRegex();
}
