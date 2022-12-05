using System.Collections.Generic;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using Xunit;

namespace API.Tests.Extensions;

public class SeriesExtensionsTests
{
    [Fact]
    public void GetCoverImage_MultipleSpecials_Comics()
    {
        var series = new Series()
        {
            Name = "Test 1",
            NormalizedName = "Test 1".Normalize(),
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
            Name = "Test 1",
            NormalizedName = "Test 1".Normalize(),
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
            Name = "Test 1",
            NormalizedName = "Test 1".Normalize(),
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
            Name = "Test 1",
            NormalizedName = "Test 1".Normalize(),
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
            Name = "Test 1",
            NormalizedName = "Test 1".Normalize(),
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
            Name = "Test 1",
            NormalizedName = "Test 1".Normalize(),
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
