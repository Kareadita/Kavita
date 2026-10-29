using System.Collections.Generic;
using System.Linq;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using static Kavita.Services.DebugExport.ExportScrubCatalog;

namespace Kavita.Services.Tests.DebugExport;

public class ExportScrubCatalogTests
{
    private static readonly IReadOnlyList<Entry> Entries = Classify(CreateModel());

    [Fact]
    public void EveryTextColumnIsReviewed()
    {
        AssertNoneUnreviewed(Area.Column);
    }

    [Fact]
    public void EveryTextJsonFieldIsReviewed()
    {
        AssertNoneUnreviewed(Area.JsonField);
    }

    [Fact]
    public void EveryServerSettingKeyIsReviewed()
    {
        AssertNoneUnreviewed(Area.ServerSetting);
    }

    [Fact]
    public void ReviewedListHasNoStaleOrConflictingEntries()
    {
        var byName = Entries.ToDictionary(e => e.Name);
        var stale = ReviewedTextFields.Names.Where(n => !byName.ContainsKey(n));
        var conflicting = ReviewedTextFields.Names.Where(n => byName.TryGetValue(n, out var e) && (e.Source != Source.Unmarked || !e.IsText));

        Assert.True(!stale.Any() && !conflicting.Any(),
            $"No longer exist:\n{Lines(stale)}\nScrubbed or not text, so they don't belong in the list:\n{Lines(conflicting)}");
    }

    [Theory]
    [InlineData("AppUser.ApiKey")]
    [InlineData("AppUser.PasswordHash")]
    [InlineData("AppUserAuthKey.Key")]
    [InlineData("Device.IpAddress")]
    [InlineData("ClientInfoData.IpAddress")]
    [InlineData("AppUserScrobbleProvider.AuthenticationToken")]
    [InlineData("ServerSettingKey.LicenseKey")]
    [InlineData("OidcConfigDto.Secret")]
    [InlineData("DataProtectionKey.Xml")]
    public void KnownSecretsAreScrubbed(string name)
    {
        var entry = Entries.SingleOrDefault(e => e.Name == name);

        Assert.NotNull(entry);
        Assert.NotNull(entry.Kind);
    }

    private static void AssertNoneUnreviewed(Area area)
    {
        var unreviewed = Entries
            .Where(e => e.Area == area && e.IsText && e.Source == Source.Unmarked && !ReviewedTextFields.Names.Contains(e.Name))
            .Select(e => e.Name)
            .ToList();

        Assert.True(unreviewed.Count == 0,
            $"{unreviewed.Count} new text field(s) would be exported as is. Add [ExportScrub], or if safe, add to ReviewedTextFields:\n{Lines(unreviewed)}");
    }

    private static string Lines(IEnumerable<string> names) =>
        string.Join("\n", names.Order().Select(n => $"        \"{n}\","));

    private static IModel CreateModel()
    {
        using var context = new DataContext(new DbContextOptionsBuilder<DataContext>().UseSqlite("Data Source=:memory:").Options);
        return context.Model;
    }
}
