using System.Collections.Generic;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Parser;
using API.Services.Tasks.Scanner;
using Xunit;

namespace API.Tests.Extensions;

public class SeriesExtensionsTests
{
    [Theory]
    [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker than Black"}, true)]
    [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker_than_Black"}, true)]
    [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker then Black!"}, false)]
    [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"Salem's Lot"}, true)]
    [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"salems lot"}, true)]
    [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"salem's lot"}, true)]
    // Different normalizations pass as we check normalization against an on-the-fly calculation so we don't delete series just because we change how normalization works
    [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot", "salems lot"}, new [] {"salem's lot"}, true)]
    [InlineData(new [] {"Rent-a-Girlfriend", "Rent-a-Girlfriend", "Kanojo, Okarishimasu", "rentagirlfriend"}, new [] {"Kanojo, Okarishimasu"}, true)]
    public void NameInListTest(string[] seriesInput, string[] list, bool expected)
    {
        var series = new Series()
        {
            Name = seriesInput[0],
            LocalizedName = seriesInput[1],
            OriginalName = seriesInput[2],
            NormalizedName = seriesInput.Length == 4 ? seriesInput[3] : API.Services.Tasks.Scanner.Parser.Parser.Normalize(seriesInput[0]),
            Metadata = new SeriesMetadata()
        };

        Assert.Equal(expected, series.NameInList(list));
    }

    [Theory]
    [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker than Black"}, MangaFormat.Archive, true)]
    [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker_than_Black"}, MangaFormat.Archive, true)]
    [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker then Black!"}, MangaFormat.Archive, false)]
    [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"Salem's Lot"}, MangaFormat.Archive, true)]
    [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"salems lot"}, MangaFormat.Archive, true)]
    [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"salem's lot"}, MangaFormat.Archive, true)]
    // Different normalizations pass as we check normalization against an on-the-fly calculation so we don't delete series just because we change how normalization works
    [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot", "salems lot"}, new [] {"salem's lot"}, MangaFormat.Archive, true)]
    [InlineData(new [] {"Rent-a-Girlfriend", "Rent-a-Girlfriend", "Kanojo, Okarishimasu", "rentagirlfriend"}, new [] {"Kanojo, Okarishimasu"}, MangaFormat.Archive, true)]
    public void NameInListParserInfoTest(string[] seriesInput, string[] list, MangaFormat format, bool expected)
    {
        var series = new Series()
        {
            Name = seriesInput[0],
            LocalizedName = seriesInput[1],
            OriginalName = seriesInput[2],
            NormalizedName = seriesInput.Length == 4 ? seriesInput[3] : API.Services.Tasks.Scanner.Parser.Parser.Normalize(seriesInput[0]),
            Metadata = new SeriesMetadata(),
        };

        var parserInfos = list.Select(s => new ParsedSeries()
        {
            Name = s,
            NormalizedName = API.Services.Tasks.Scanner.Parser.Parser.Normalize(s),
        }).ToList();

        // This doesn't do any checks against format
        Assert.Equal(expected, series.NameInList(parserInfos));
    }


    [Theory]
    [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, "Darker than Black", true)]
    [InlineData(new [] {"Rent-a-Girlfriend", "Rent-a-Girlfriend", "Kanojo, Okarishimasu", "rentagirlfriend"}, "Kanojo, Okarishimasu", true)]
    [InlineData(new [] {"Rent-a-Girlfriend", "Rent-a-Girlfriend", "Kanojo, Okarishimasu", "rentagirlfriend"}, "Rent", false)]
    public void NameInParserInfoTest(string[] seriesInput, string parserSeries, bool expected)
    {
        var series = new Series()
        {
            Name = seriesInput[0],
            LocalizedName = seriesInput[1],
            OriginalName = seriesInput[2],
            NormalizedName = seriesInput.Length == 4 ? seriesInput[3] : API.Services.Tasks.Scanner.Parser.Parser.Normalize(seriesInput[0]),
            Metadata = new SeriesMetadata()
        };
        var info = new ParserInfo
        {
            Series = parserSeries
        };

        Assert.Equal(expected, series.NameInParserInfo(info));
    }

    [Fact]
    public void GetCoverImage_MultipleSpecials_Comics()
    {
        var series = new Series()
        {
            Format = MangaFormat.Archive,
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Number = 0,
                    Name = API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            IsSpecial = true,
                            Number = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
                            CoverImage = "Special 1",
                        },
                        new Chapter()
                        {
                            IsSpecial = true,
                            Number = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
                            CoverImage = "Special 2",
                        }
                    },
                }
            }
        };

        Assert.Equal("Special 1", series.GetCoverImage());

    }

    [Fact]
    public void GetCoverImage_MultipleSpecials_Books()
    {
        var series = new Series()
        {
            Format = MangaFormat.Epub,
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Number = 0,
                    Name = API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            IsSpecial = true,
                            Number = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
                            CoverImage = "Special 1",
                        },
                        new Chapter()
                        {
                            IsSpecial = true,
                            Number = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
                            CoverImage = "Special 2",
                        }
                    },
                }
            }
        };

        Assert.Equal("Special 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustChapters_Comics()
    {
        var series = new Series()
        {
            Format = MangaFormat.Archive,
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Number = 0,
                    Name = API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "2.5",
                            CoverImage = "Special 1",
                        },
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "2",
                            CoverImage = "Special 2",
                        }
                    },
                }
            }
        };

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Special 2", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_JustChaptersAndSpecials_Comics()
    {
        var series = new Series()
        {
            Format = MangaFormat.Archive,
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Number = 0,
                    Name = API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "2.5",
                            CoverImage = "Special 1",
                        },
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "2",
                            CoverImage = "Special 2",
                        },
                        new Chapter()
                        {
                            IsSpecial = true,
                            Number = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
                            CoverImage = "Special 3",
                        }
                    },
                }
            }
        };

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Special 2", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChapters_Comics()
    {
        var series = new Series()
        {
            Format = MangaFormat.Archive,
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Number = 0,
                    Name = API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "2.5",
                            CoverImage = "Special 1",
                        },
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "2",
                            CoverImage = "Special 2",
                        },
                        new Chapter()
                        {
                            IsSpecial = true,
                            Number = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
                            CoverImage = "Special 3",
                        }
                    },
                },
                new Volume()
                {
                    Number = 1,
                    Name = "1",
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "0",
                            CoverImage = "Volume 1",
                        },

                    },
                }
            }
        };

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1", series.GetCoverImage());
    }

    [Fact]
    public void GetCoverImage_VolumesChaptersAndSpecials_Comics()
    {
        var series = new Series()
        {
            Format = MangaFormat.Archive,
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Number = 0,
                    Name = API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "2.5",
                            CoverImage = "Special 1",
                        },
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "2",
                            CoverImage = "Special 2",
                        },
                        new Chapter()
                        {
                            IsSpecial = true,
                            Number = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
                            CoverImage = "Special 3",
                        }
                    },
                },
                new Volume()
                {
                    Number = 1,
                    Name = "1",
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            IsSpecial = false,
                            Number = "0",
                            CoverImage = "Volume 1",
                        },

                    },
                }
            }
        };

        foreach (var vol in series.Volumes)
        {
            vol.CoverImage = vol.Chapters.MinBy(x => double.Parse(x.Number), ChapterSortComparerZeroFirst.Default)?.CoverImage;
        }

        Assert.Equal("Volume 1", series.GetCoverImage());
    }


}
