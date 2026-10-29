using System.Collections.Generic;
using Kavita.Models.DebugExport;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Tests.DebugExport;

public class ExportTextScrubberTests
{
    private const string AuthKey = "a1b2c3d4e5f6";
    private const string JoeIp = "82.123.6.194";

    private readonly ExportJunk _junk = new();
    private readonly Dictionary<string, string> _usernames = [];

    public ExportTextScrubberTests()
    {
        _junk.Replace(ExportScrubKind.Random, AuthKey);
        _junk.Replace(ExportScrubKind.IpAddress, JoeIp);
        AddUser("joe", "user-1");
        AddUser("al", "user-2");
    }

    [Fact]
    public void KnownValueIsReplacedInsideAPath()
    {
        var scrubbed = Scrubber().Scrub($"GET /api/koreader/{AuthKey}/syncs/progress");

        Assert.Equal($"GET /api/koreader/{_junk.Replaced[AuthKey]}/syncs/progress", scrubbed);
    }

    [Fact]
    public void UsernameIsReplacedOutsidePathsOnly()
    {
        var scrubbed = Scrubber().Scrub("[joe] started scan of /home/joe/manga");

        Assert.Equal("[user-1] started scan of /home/joe/manga", scrubbed);
    }

    [Fact]
    public void ShortUsernameMatchesWholeWordsOnly()
    {
        var scrubbed = Scrubber().Scrub("[al] read it, also Alan");

        Assert.Equal("[user-2] read it, also Alan", scrubbed);
    }

    [Fact]
    public void IpSeenInDatabaseGetsSameJunk()
    {
        var scrubbed = Scrubber().Scrub($"login from {JoeIp}");

        Assert.Equal($"login from {_junk.Replaced[JoeIp]}", scrubbed);
    }

    [Fact]
    public void UnknownEmailAndIpGetJunkLikeTheDatabase()
    {
        var scrubbed = Scrubber().Scrub("550 bob@x.com: mailbox unavailable, relay 93.184.216.34");

        Assert.Equal($"550 {_junk.Replaced["bob@x.com"]}: mailbox unavailable, relay {_junk.Replaced["93.184.216.34"]}", scrubbed);
    }

    [Theory]
    [InlineData("[Kavita] - v0.9.1.16 starting")]
    [InlineData("Listening on 127.0.0.1:5000 and [::1]:5000")]
    [InlineData("2026-10-29 12:34:56.789 -04:00 [INF] Took 0.0.0.1 ms")]
    public void VersionsLoopbackAndTimesAreKept(string line)
    {
        Assert.Equal(line, Scrubber().Scrub(line));
    }

    [Fact]
    public void UnknownTokenInUrlIsReplaced()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJlX2hlcmU";
        const string apiKey = "NotInTheDatabase99";
        var line = $"GET https://x.invalid/api/v3/want-to-read?provider=AniList&accessToken={jwt}&apiKey={apiKey}&page=2";

        var scrubbed = Scrubber().Scrub(line);

        Assert.DoesNotContain(jwt, scrubbed);
        Assert.DoesNotContain(apiKey, scrubbed);
        Assert.EndsWith("&page=2", scrubbed);
        Assert.Equal(line.Length, scrubbed.Length);
    }

    private ExportTextScrubber Scrubber() => new(_junk, _usernames);

    private void AddUser(string name, string owner)
    {
        _usernames[name] = _junk.Replace(ExportScrubKind.Username, name, owner)!;
    }
}
