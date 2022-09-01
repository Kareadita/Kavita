using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Helpers;
using API.Parser;
using API.Services.Tasks.Scanner;
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

        var series = new Series()
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
        };

        Assert.False(ParserInfoHelpers.SeriesHasMatchingParserInfoFormat(series, infos));
    }

    [Fact]
    public void SeriesHasMatchingParserInfoFormat_ShouldBeTrue()
    {
        var infos = new Dictionary<ParsedSeries, IList<ParserInfo>>();

        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Archive});
        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Epub});

        var series = new Series()
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
        };

        Assert.True(ParserInfoHelpers.SeriesHasMatchingParserInfoFormat(series, infos));
    }

    #endregion
}
