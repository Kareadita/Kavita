using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Parser;
using API.Services.Tasks;
using API.Services.Tasks.Scanner;
using API.Tests.Helpers;
using Xunit;

namespace API.Tests.Services;

public class ScannerServiceTests
{
    [Fact]
    public void FindSeriesNotOnDisk_Should_Remove1()
    {
        var infos = new Dictionary<ParsedSeries, IList<ParserInfo>>();

        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Archive});
        //AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Epub});

        var existingSeries = new List<Series>
        {
            new Series()
            {
                Name = "Darker Than Black",
                LocalizedName = "Darker Than Black",
                OriginalName = "Darker Than Black",
                Volumes = new List<Volume>()
                {
                    new Volume()
                    {
                        Number = 1,
                        Name = "1"
                    }
                },
                NormalizedName = API.Services.Tasks.Scanner.Parser.Parser.Normalize("Darker Than Black"),
                Metadata = new SeriesMetadata(),
                Format = MangaFormat.Epub
            }
        };

        Assert.Equal(1, ScannerService.FindSeriesNotOnDisk(existingSeries, infos).Count());
    }

    [Fact]
    public void FindSeriesNotOnDisk_Should_RemoveNothing_Test()
    {
        var infos = new Dictionary<ParsedSeries, IList<ParserInfo>>();

        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Format = MangaFormat.Archive});
        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "1", Format = MangaFormat.Archive});
        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "10", Format = MangaFormat.Archive});

        var existingSeries = new List<Series>
        {
            new Series()
            {
                Name = "Cage of Eden",
                LocalizedName = "Cage of Eden",
                OriginalName = "Cage of Eden",
                NormalizedName = API.Services.Tasks.Scanner.Parser.Parser.Normalize("Cage of Eden"),
                Metadata = new SeriesMetadata(),
                Format = MangaFormat.Archive
            },
            new Series()
            {
                Name = "Darker Than Black",
                LocalizedName = "Darker Than Black",
                OriginalName = "Darker Than Black",
                NormalizedName = API.Services.Tasks.Scanner.Parser.Parser.Normalize("Darker Than Black"),
                Metadata = new SeriesMetadata(),
                Format = MangaFormat.Archive
            }
        };



        Assert.Empty(ScannerService.FindSeriesNotOnDisk(existingSeries, infos));
    }


    // TODO: Figure out how to do this with ParseScannedFiles
    // [Theory]
    // [InlineData(new [] {"Darker than Black"}, "Darker than Black", "Darker than Black")]
    // [InlineData(new [] {"Darker than Black"}, "Darker Than Black", "Darker than Black")]
    // [InlineData(new [] {"Darker than Black"}, "Darker Than Black!", "Darker than Black")]
    // [InlineData(new [] {""}, "Runaway Jack", "Runaway Jack")]
    // public void MergeNameTest(string[] existingSeriesNames, string parsedInfoName, string expected)
    // {
    //     var collectedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
    //     foreach (var seriesName in existingSeriesNames)
    //     {
    //         AddToParsedInfo(collectedSeries, new ParserInfo() {Series = seriesName, Format = MangaFormat.Archive});
    //     }
    //
    //     var actualName = new ParseScannedFiles(_bookService, _logger).MergeName(collectedSeries, new ParserInfo()
    //     {
    //         Series = parsedInfoName,
    //         Format = MangaFormat.Archive
    //     });
    //
    //     Assert.Equal(expected, actualName);
    // }

    // [Fact]
    // public void RemoveMissingSeries_Should_RemoveSeries()
    // {
    //     var existingSeries = new List<Series>()
    //     {
    //         EntityFactory.CreateSeries("Darker than Black Vol 1"),
    //         EntityFactory.CreateSeries("Darker than Black"),
    //         EntityFactory.CreateSeries("Beastars"),
    //     };
    //     var missingSeries = new List<Series>()
    //     {
    //         EntityFactory.CreateSeries("Darker than Black Vol 1"),
    //     };
    //     existingSeries = ScannerService.RemoveMissingSeries(existingSeries, missingSeries, out var removeCount).ToList();
    //
    //     Assert.DoesNotContain(missingSeries[0].Name, existingSeries.Select(s => s.Name));
    //     Assert.Equal(missingSeries.Count, removeCount);
    // }


    // TODO: I want a test for UpdateSeries where if I have chapter 10 and now it's mapping into Vol 2 Chapter 10,
    // if I can do it without deleting the underlying chapter (aka id change)

}
