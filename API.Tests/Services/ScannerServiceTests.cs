using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
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
                NormalizedName = "Darker Than Black".ToNormalized(),
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
                NormalizedName = "Darker Than Black".ToNormalized(),
                Metadata = new SeriesMetadata(),
                Format = MangaFormat.Archive
            },
            new Series()
            {
                Name = "Darker Than Black",
                LocalizedName = "Darker Than Black",
                OriginalName = "Darker Than Black",
                NormalizedName = "Darker Than Black".ToNormalized(),
                Metadata = new SeriesMetadata(),
                Format = MangaFormat.Archive
            }
        };

        Assert.Empty(ScannerService.FindSeriesNotOnDisk(existingSeries, infos));
    }
}
