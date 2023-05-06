using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;
using Xunit;

namespace API.Tests.Helpers;

public class ParserInfoHelperTests
{
    #region SeriesHasMatchingParserInfoFormat

    [Fact]
    public void SeriesHasMatchingParserInfoFormat_ShouldBeFalse()
    {
        var infos = new Dictionary<ParsedSeries, IList<ParserInfo>>();

        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Archive});
        //AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Epub});

        var series = new SeriesBuilder("Darker Than Black")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithName("1")
                .Build())
            .WithLocalizedName("Darker Than Black")
            .Build();

        Assert.False(ParserInfoHelpers.SeriesHasMatchingParserInfoFormat(series, infos));
    }

    [Fact]
    public void SeriesHasMatchingParserInfoFormat_ShouldBeTrue()
    {
        var infos = new Dictionary<ParsedSeries, IList<ParserInfo>>();

        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Archive});
        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Epub});


        var series = new SeriesBuilder("Darker Than Black")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithName("1")
                .Build())
            .WithLocalizedName("Darker Than Black")
            .Build();

        Assert.True(ParserInfoHelpers.SeriesHasMatchingParserInfoFormat(series, infos));
    }

    #endregion
}
