using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Services.Helpers;
using Kavita.Services.ReadingLists;

namespace Kavita.Services.Tests.ReadingLists;

public class CblParserTests
{
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/CblParserTests/Test Cases");

    #region V1 Spec

    [Fact]
    public void ParseV1Test_NoSpecial()
    {
        const string filename = "[DC Comics] Aquaman- Death of a Prince (WEB-CBRO).cbl";
        var result = CblParser.ParseV1(Path.Join(_testDirectory, filename));

        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal("[DC Comics] Aquaman- Death of a Prince (WEB-CBRO)", result.Name);
        Assert.Empty(result.Uuid);
        Assert.Equal(CblListType.Unknown, result.ListType);
        Assert.Equal(-1, result.StartYear);
        Assert.Equal(-1, result.EndYear);

        Assert.Equal(25, result.Items.Count);

        // First item
        var first = result.Items[0];
        Assert.Equal(0, first.Order);
        Assert.Equal("Adventure Comics", first.SeriesName);
        Assert.Equal("435", first.Number);
        Assert.Equal("1938", first.Volume);
        Assert.Equal("1974", first.Year);
        Assert.Equal(CblIssueType.Unknown, first.IssueType);
        Assert.Single(first.ExternalIds);
        Assert.Equal(CblExternalDbProvider.ComicVine, first.ExternalIds[0].Provider);
        Assert.Equal("3105", first.ExternalIds[0].SeriesId);
        Assert.Equal("124869", first.ExternalIds[0].IssueId);

        // Last item (Aquaman #63)
        var last = result.Items[24];
        Assert.Equal(24, last.Order);
        Assert.Equal("Aquaman", last.SeriesName);
        Assert.Equal("63", last.Number);
        Assert.Equal("1962", last.Volume);
        Assert.Equal("1978", last.Year);
        Assert.Equal(CblExternalDbProvider.ComicVine, last.ExternalIds[0].Provider);
        Assert.Equal("2050", last.ExternalIds[0].SeriesId);
        Assert.Equal("137565", last.ExternalIds[0].IssueId);

        // Item 15 (transition from Adventure Comics to Aquaman)
        var item15 = result.Items[15];
        Assert.Equal("Aquaman", item15.SeriesName);
        Assert.Equal("57", item15.Number);
    }

    [Fact]
    public void ParseV1Test_Special()
    {
        const string filename = "BOOM! Power Rangers Simplified 1a.cbl";
        var result = CblParser.ParseV1(Path.Join(_testDirectory, filename));

        Assert.Equal("Simplified Power Rangers 1a", result.Name);
        Assert.Equal(164, result.Items.Count);

        // First item
        var first = result.Items[0];
        Assert.Equal("Mighty Morphin Power Rangers", first.SeriesName);
        Assert.Equal("0", first.Number);
        Assert.Equal("2016", first.Volume);
        Assert.Equal("2016", first.Year);
        Assert.Single(first.ExternalIds);
        Assert.Equal(CblExternalDbProvider.ComicVine, first.ExternalIds[0].Provider);
        Assert.Equal("87332", first.ExternalIds[0].SeriesId);
        Assert.Equal("511002", first.ExternalIds[0].IssueId);

        // Last item
        var last = result.Items[163];
        Assert.Equal("Power Rangers Unlimited: The Morphin Masters", last.SeriesName);
        Assert.Equal("1", last.Number);
        Assert.Equal("2024", last.Volume);
    }

    [Fact]
    public void ParseV1Test_DatabaseElementCaptured()
    {
        const string filename = "[DC Comics] Aquaman- Death of a Prince (WEB-CBRO).cbl";
        var result = CblParser.ParseV1(Path.Join(_testDirectory, filename));

        // Every item in this file has a Database element
        foreach (var item in result.Items)
        {
            Assert.Single(item.ExternalIds);
            Assert.Equal(CblExternalDbProvider.ComicVine, item.ExternalIds[0].Provider);
            Assert.False(string.IsNullOrEmpty(item.ExternalIds[0].SeriesId));
            Assert.False(string.IsNullOrEmpty(item.ExternalIds[0].IssueId));
        }
    }

    #endregion


    #region V2 Spec

    [Fact]
    public void ParseV2Test()
    {
        const string filename = "2018-2021 Part 16.1 Reborn Again.json";
        var result = CblParser.ParseV2(Path.Join(_testDirectory, filename));

        // File details
        Assert.Equal("a59e4ad5-0d51-4afe-b0f5-6d3e01eb7cc7", result.Uuid);
        Assert.Equal(1, result.SchemaVersion);

        // List details
        Assert.Equal("Part 16.1 Reborn Again", result.Name);
        Assert.Equal(2021, result.StartYear);
        Assert.Equal(2022, result.EndYear);
        Assert.Equal("Marvel", result.Publisher);
        Assert.Equal(CblListType.Universal, result.ListType);

        // Tags
        Assert.Equal(3, result.Tags.Count);
        Assert.Contains("avengers", result.Tags);
        Assert.Contains("marvel guides", result.Tags);
        Assert.Contains("fresh start", result.Tags);

        // Relationships
        Assert.Equal(2, result.Relationships.Count);
        var prev = result.Relationships[0];
        Assert.Equal("Part 15.4 Trial by Fire", prev.Name);
        Assert.Equal("e1eecedf-df97-4ab4-a476-ee85204e3b78", prev.Uuid);
        Assert.Equal("previous", prev.Relationship);
        var next = result.Relationships[1];
        Assert.Equal("Part 16.2 Sinister War", next.Name);
        Assert.Equal("507e7444-8f5f-46fc-a2cc-17f08c000982", next.Uuid);
        Assert.Equal("following", next.Relationship);

        // Sources
        Assert.Single(result.Sources);
        Assert.Equal("Marvel Guides", result.Sources[0].Name);
        Assert.Equal("https://marvelguides.com/fresh-start-finale-reading-order", result.Sources[0].Url);

        // Issues
        Assert.Equal(58, result.Items.Count);

        // First issue - Heroes Reborn #1
        var first = result.Items[0];
        Assert.Equal(0, first.Order);
        Assert.Equal("Heroes Reborn", first.SeriesName);
        Assert.Equal("1", first.Number);
        Assert.Equal("2021", first.Volume);
        Assert.Equal("2021", first.Year);
        Assert.Equal("2021-07-01", first.CoverDate);
        Assert.Equal(CblIssueType.Unknown, first.IssueType);
        Assert.Equal(2, first.ExternalIds.Count);
        Assert.Equal(CblExternalDbProvider.ComicVine, first.ExternalIds[0].Provider);
        Assert.Equal("135903", first.ExternalIds[0].SeriesId);
        Assert.Equal("847511", first.ExternalIds[0].IssueId);
        Assert.Equal(CblExternalDbProvider.Metron, first.ExternalIds[1].Provider);
        Assert.Equal("2139", first.ExternalIds[1].SeriesId);
        Assert.Equal("29926", first.ExternalIds[1].IssueId);

        // Black Cat #8 (item 22) - only has comicvine, no metron
        var blackCat = result.Items[22];
        Assert.Equal("Black Cat", blackCat.SeriesName);
        Assert.Equal("8", blackCat.Number);
        Assert.Equal("2020", blackCat.Volume);
        Assert.Single(blackCat.ExternalIds);
        Assert.Equal(CblExternalDbProvider.ComicVine, blackCat.ExternalIds[0].Provider);

        // Giant-Sized Black Cat (item 25) - no coverDate
        var giantSized = result.Items[25];
        Assert.Equal("Giant-Sized Black Cat: Infinity Score", giantSized.SeriesName);
        Assert.Empty(giantSized.CoverDate);
        Assert.Empty(giantSized.Year);
    }

    [Fact]
    public void ParseV2Test_AutoDetect()
    {
        const string filename = "2018-2021 Part 16.1 Reborn Again.json";
        var result = CblParser.Parse(Path.Join(_testDirectory, filename));

        Assert.Equal("a59e4ad5-0d51-4afe-b0f5-6d3e01eb7cc7", result.Uuid);
        Assert.Equal("Part 16.1 Reborn Again", result.Name);
        Assert.Equal(58, result.Items.Count);
    }

    [Fact]
    public void ParseV1Test_AutoDetect()
    {
        const string filename = "[DC Comics] Aquaman- Death of a Prince (WEB-CBRO).cbl";
        var result = CblParser.Parse(Path.Join(_testDirectory, filename));

        Assert.Equal("[DC Comics] Aquaman- Death of a Prince (WEB-CBRO)", result.Name);
        Assert.Equal(25, result.Items.Count);
    }

    #endregion
}
