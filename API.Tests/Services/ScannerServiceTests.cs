using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers.Builders;
using API.Services.Tasks;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;
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
            new SeriesBuilder("Darker Than Black")
                .WithFormat(MangaFormat.Epub)

                .WithVolume(new VolumeBuilder("1")
                .WithName("1")
                .Build())
                .WithLocalizedName("Darker Than Black")
                .Build()
        };

        Assert.Single(ScannerService.FindSeriesNotOnDisk(existingSeries, infos));
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
            new SeriesBuilder("Cage of Eden")
                .WithFormat(MangaFormat.Archive)

                .WithVolume(new VolumeBuilder("1")
                    .WithName("1")
                    .Build())
                .WithLocalizedName("Darker Than Black")
                .Build(),
            new SeriesBuilder("Darker Than Black")
                .WithFormat(MangaFormat.Archive)
                .WithVolume(new VolumeBuilder("1")
                    .WithName("1")
                    .Build())
                .WithLocalizedName("Darker Than Black")
                .Build(),
        };

        Assert.Empty(ScannerService.FindSeriesNotOnDisk(existingSeries, infos));
    }
}
