using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Services.Helpers;
using Kavita.Services.ReadingLists;

namespace Kavita.Services.Tests.ReadingLists;

public class CblExportServiceTests
{
    #region Helpers

    private static ReadingList CreateReadingList(string title = "Test List", string? summary = "A test reading list",
        int startingYear = 2020, int startingMonth = 1, int endingYear = 2021, int endingMonth = 12)
    {
        return new ReadingList
        {
            Id = 1,
            Title = title,
            NormalizedTitle = title.ToLower(),
            Summary = summary,
            AgeRating = AgeRating.Unknown,
            StartingYear = startingYear,
            StartingMonth = startingMonth,
            EndingYear = endingYear,
            EndingMonth = endingMonth,
        };
    }

    private static ReadingListItem CreateItem(int order, string seriesName, string chapterRange,
        string volumeName, DateTime? releaseDate = null, bool isSpecial = false,
        MangaFormat format = MangaFormat.Archive,
        string? comicVineId = null, long metronId = 0, int aniListId = 0, long malId = 0, int hardcoverId = 0)
    {
        var series = new Series
        {
            Id = order + 1,
            Name = seriesName,
            NormalizedName = seriesName.ToLower(),
            NormalizedLocalizedName = seriesName.ToLower(),
            SortName = seriesName,
            LocalizedName = seriesName,
            OriginalName = seriesName,
            Format = format,
            Metadata = new SeriesMetadata
            {
                People = new List<SeriesMetadataPeople>(),
            },
        };

        return new ReadingListItem
        {
            Order = order,
            Series = series,
            SeriesId = series.Id,
            Volume = new Volume
            {
                Name = volumeName,
                MinNumber = 0,
                MaxNumber = 0,
                LookupName = volumeName,
            },
            Chapter = new Chapter
            {
                Range = chapterRange,
                IsSpecial = isSpecial,
                ReleaseDate = releaseDate ?? DateTime.MinValue,
                ComicVineId = comicVineId,
                MetronId = metronId,
                AniListId = aniListId,
                MalId = malId,
                HardcoverId = hardcoverId,
            },
        };
    }

    #endregion

    #region BuildCblReadingList

    [Fact]
    public void ExportV1_BasicReadingList()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016", new DateTime(2016, 6, 15)),
            CreateItem(1, "Batman", "2", "2016", new DateTime(2016, 7, 6)),
            CreateItem(2, "Superman", "10", "2011", new DateTime(2013, 3, 1)),
        };

        var result = CblExportService.BuildCblReadingList(readingList, items);

        Assert.Equal("Test List", result.Name);
        Assert.Equal("A test reading list", result.Summary);
        Assert.Equal(2020, result.StartYear);
        Assert.Equal(1, result.StartMonth);
        Assert.Equal(2021, result.EndYear);
        Assert.Equal(12, result.EndMonth);

        Assert.Equal(3, result.Books.Book.Count);

        var first = result.Books.Book[0];
        Assert.Equal("Batman", first.Series);
        Assert.Equal("1", first.Number);
        Assert.Equal("2016", first.Volume);
        Assert.Equal("2016", first.Year);
        Assert.Equal(string.Empty, first.Format);
        Assert.Equal("cbz", first.FileType);
        Assert.Single(first.Databases);
        Assert.Equal("kavita", first.Databases[0].Name);
        Assert.Equal("1", first.Databases[0].Series);
        Assert.Equal("0", first.Databases[0].Issue);

        var last = result.Books.Book[2];
        Assert.Equal("Superman", last.Series);
        Assert.Equal("10", last.Number);
        Assert.Equal("2011", last.Volume);
        Assert.Equal("2013", last.Year);
    }

    [Fact]
    public void ExportV1_SpecialChapter()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "Annual 1", "2016", isSpecial: true),
        };

        var result = CblExportService.BuildCblReadingList(readingList, items);

        Assert.Single(result.Books.Book);
        Assert.Equal("Annual", result.Books.Book[0].Format);
    }

    [Theory]
    [InlineData(MangaFormat.Archive, "cbz")]
    [InlineData(MangaFormat.Epub, "epub")]
    [InlineData(MangaFormat.Pdf, "pdf")]
    [InlineData(MangaFormat.Image, "image")]
    [InlineData(MangaFormat.Unknown, "")]
    public void ExportV1_FileTypeMappings(MangaFormat format, string expected)
    {
        Assert.Equal(expected, CblExportService.MapMangaFormatToFileType(format));
    }

    [Fact]
    public void ExportV1_EmptyItems()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>();

        var result = CblExportService.BuildCblReadingList(readingList, items);

        Assert.Equal("Test List", result.Name);
        Assert.Empty(result.Books.Book);
    }

    [Fact]
    public void ExportV1_DefaultReleaseDate_EmptyYear()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016"),
        };

        var result = CblExportService.BuildCblReadingList(readingList, items);

        Assert.Equal(string.Empty, result.Books.Book[0].Year);
    }

    [Fact]
    public void ExportV1_ExternalIds_SingleProvider()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016", new DateTime(2016, 6, 15), comicVineId: "cv-12345"),
        };

        var result = CblExportService.BuildCblReadingList(readingList, items);

        var first = result.Books.Book[0];
        Assert.Equal(2, first.Databases.Count);
        // Kavita entry is always first
        Assert.Equal("kavita", first.Databases[0].Name);
        Assert.Equal("1", first.Databases[0].Series);
        Assert.Equal("0", first.Databases[0].Issue);
        // ComicVine entry
        Assert.Equal("cv", first.Databases[1].Name);
        Assert.Null(first.Databases[1].Series); // Series is the series id and not the Series name
        Assert.Equal("cv-12345", first.Databases[1].Issue);
    }

    [Fact]
    public void ExportV1_ExternalIds_MultipleProviders()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016", new DateTime(2016, 6, 15),
                comicVineId: "cv-12345", metronId: 67890),
        };

        var result = CblExportService.BuildCblReadingList(readingList, items);

        var first = result.Books.Book[0];
        Assert.Equal(3, first.Databases.Count);
        // Kavita entry is always first
        Assert.Equal("kavita", first.Databases[0].Name);
        Assert.Equal("1", first.Databases[0].Series);
        Assert.Equal("0", first.Databases[0].Issue);
        // ComicVine and Metron follow
        Assert.Equal("cv", first.Databases[1].Name);
        Assert.Equal("cv-12345", first.Databases[1].Issue);
        Assert.Equal("metron", first.Databases[2].Name);
        Assert.Equal("67890", first.Databases[2].Issue);
    }

    [Fact]
    public void ExportV1_ExternalIds_NoIds()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016", new DateTime(2016, 6, 15)),
        };

        var result = CblExportService.BuildCblReadingList(readingList, items);

        var kavitaOnly = result.Books.Book[0].Databases;
        Assert.Single(kavitaOnly);
        Assert.Equal("kavita", kavitaOnly[0].Name);
        Assert.Equal("1", kavitaOnly[0].Series);
        Assert.Equal("0", kavitaOnly[0].Issue);
    }

    [Fact]
    public void ExportV1_ExternalIds_RoundTrip()
    {
        var readingList = CreateReadingList(title: "External ID Round Trip");
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016", new DateTime(2016, 6, 15),
                comicVineId: "cv-12345", metronId: 67890),
        };

        var cbl = CblExportService.BuildCblReadingList(readingList, items);

        var tempFile = Path.Combine(Path.GetTempPath(), $"cbl-extid-test-{Guid.NewGuid()}.cbl");
        try
        {
            CblExportService.SerializeV1(cbl, tempFile);

            var parsed = CblParser.ParseV1(tempFile);

            Assert.Single(parsed.Items);
            var item = parsed.Items[0];
            Assert.Equal(3, item.ExternalIds.Count);

            var kavita = item.ExternalIds.First(e => e.Provider == CblExternalDbProvider.Kavita);
            Assert.Equal("1", kavita.SeriesId);
            Assert.Equal("0", kavita.IssueId);

            var cv = item.ExternalIds.First(e => e.Provider == CblExternalDbProvider.ComicVine);
            Assert.Equal("cv-12345", cv.IssueId);

            var metron = item.ExternalIds.First(e => e.Provider == CblExternalDbProvider.Metron);
            Assert.Equal("67890", metron.IssueId);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    #endregion

    #region RoundTrip

    [Fact]
    public void ExportV1_RoundTrip()
    {
        var readingList = CreateReadingList(title: "Round Trip Test", summary: "Testing round trip");
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016", new DateTime(2016, 6, 15)),
            CreateItem(1, "Superman", "Annual 1", "2011", new DateTime(2013, 3, 1), isSpecial: true, format: MangaFormat.Epub),
        };

        var cbl = CblExportService.BuildCblReadingList(readingList, items);

        var tempFile = Path.Combine(Path.GetTempPath(), $"cbl-export-test-{Guid.NewGuid()}.cbl");
        try
        {
            CblExportService.SerializeV1(cbl, tempFile);

            var parsed = CblParser.ParseV1(tempFile);

            Assert.Equal("Round Trip Test", parsed.Name);
            Assert.Equal("Testing round trip", parsed.Summary);
            Assert.Equal(2020, parsed.StartYear);
            Assert.Equal(1, parsed.StartMonth);
            Assert.Equal(2021, parsed.EndYear);
            Assert.Equal(12, parsed.EndMonth);

            Assert.Equal(2, parsed.Items.Count);

            var first = parsed.Items[0];
            Assert.Equal("Batman", first.SeriesName);
            Assert.Equal("1", first.Number);
            Assert.Equal("2016", first.Volume);
            Assert.Equal("2016", first.Year);
            Assert.Equal(string.Empty, first.Format);
            Assert.Equal("cbz", first.FileType);

            var second = parsed.Items[1];
            Assert.Equal("Superman", second.SeriesName);
            Assert.Equal("Annual 1", second.Number);
            Assert.Equal("2011", second.Volume);
            Assert.Equal("2013", second.Year);
            Assert.Equal("Annual", second.Format);
            Assert.Equal("epub", second.FileType);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    #endregion

    #region BuildCblV2Root

    [Fact]
    public void ExportV2_BasicReadingList()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016", new DateTime(2016, 6, 15)),
            CreateItem(1, "Superman", "10", "2011", new DateTime(2013, 3, 1)),
        };

        // Set ReleaseYear on series metadata
        items[0].Series.Metadata.ReleaseYear = 2016;
        items[1].Series.Metadata.ReleaseYear = 2011;

        var result = CblExportService.BuildCblV2Root(readingList, items);

        Assert.NotNull(result.FileDetails);
        Assert.Equal(1.0, result.FileDetails.Version);
        Assert.False(string.IsNullOrEmpty(result.FileDetails.UUID));

        Assert.Equal("Test List", result.ListDetails.Name);
        Assert.Equal("A test reading list", result.ListDetails.Description);
        Assert.Equal(2020, result.ListDetails.StartYear);
        Assert.Equal(2021, result.ListDetails.EndYear);

        Assert.Equal(2, result.IssueList.Count);

        var first = result.IssueList[0];
        Assert.Equal("Batman", first.SeriesName);
        Assert.Equal("1", first.IssueNumber);
        Assert.Equal(2016, first.SeriesStartYear);
        Assert.Equal("2016-06-15", first.IssueCoverDate);
        Assert.Single(first.Id);
        Assert.Equal("kavita", first.Id[0].Name);
        Assert.Equal("1", first.Id[0].Series);
        Assert.Equal("0", first.Id[0].Issue);

        var second = result.IssueList[1];
        Assert.Equal("Superman", second.SeriesName);
        Assert.Equal("10", second.IssueNumber);
        Assert.Equal(2011, second.SeriesStartYear);
        Assert.Equal("2013-03-01", second.IssueCoverDate);
        Assert.Single(second.Id);
        Assert.Equal("kavita", second.Id[0].Name);
        Assert.Equal("2", second.Id[0].Series);
        Assert.Equal("0", second.Id[0].Issue);
    }

    [Fact]
    public void ExportV2_EmptyItems()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>();

        var result = CblExportService.BuildCblV2Root(readingList, items);

        Assert.Equal("Test List", result.ListDetails.Name);
        Assert.Empty(result.IssueList);
        Assert.Equal(string.Empty, result.ListDetails.Publisher);
        Assert.Equal(string.Empty, result.ListDetails.Imprint);
    }

    [Fact]
    public void ExportV2_DefaultReleaseDate_EmptyCoverDate()
    {
        var readingList = CreateReadingList();
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016"),
        };

        var result = CblExportService.BuildCblV2Root(readingList, items);

        Assert.Equal(string.Empty, result.IssueList[0].IssueCoverDate);
        Assert.Null(result.IssueList[0].SeriesStartYear);
    }

    [Fact]
    public void ExportV2_PublisherFromMostCommonPerson()
    {
        var readingList = CreateReadingList();
        var publisher = new Person
        {
            Id = 1,
            Name = "Marvel",
            NormalizedName = "marvel",
            Description = string.Empty,
            PrimaryColor = string.Empty,
            SecondaryColor = string.Empty,
        };

        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Spider-Man", "1", "2018"),
            CreateItem(1, "Avengers", "1", "2018"),
        };

        items[0].Series.Metadata.People = new List<SeriesMetadataPeople>
        {
            new() { Role = PersonRole.Publisher, Person = publisher },
        };
        items[1].Series.Metadata.People = new List<SeriesMetadataPeople>
        {
            new() { Role = PersonRole.Publisher, Person = publisher },
        };

        var result = CblExportService.BuildCblV2Root(readingList, items);

        Assert.Equal("Marvel", result.ListDetails.Publisher);
    }

    [Fact]
    public void ExportV2_RoundTrip()
    {
        var readingList = CreateReadingList(title: "V2 Round Trip", summary: "Testing V2 round trip");
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Batman", "1", "2016", new DateTime(2016, 6, 15)),
            CreateItem(1, "Superman", "10", "2011", new DateTime(2013, 3, 1)),
        };
        items[0].Series.Metadata.ReleaseYear = 2016;
        items[1].Series.Metadata.ReleaseYear = 2011;

        var v2 = CblExportService.BuildCblV2Root(readingList, items);

        var tempFile = Path.Combine(Path.GetTempPath(), $"cbl-export-test-{Guid.NewGuid()}.json");
        try
        {
            CblExportService.SerializeV2(v2, tempFile);

            var parsed = CblParser.ParseV2(tempFile);

            Assert.Equal("V2 Round Trip", parsed.Name);
            Assert.Equal("Testing V2 round trip", parsed.Summary);
            Assert.Equal(2020, parsed.StartYear);
            Assert.Equal(2021, parsed.EndYear);

            Assert.Equal(2, parsed.Items.Count);

            var first = parsed.Items[0];
            Assert.Equal("Batman", first.SeriesName);
            Assert.Equal("1", first.Number);
            Assert.Equal("2016", first.Volume);
            Assert.Equal("2016", first.Year);

            var second = parsed.Items[1];
            Assert.Equal("Superman", second.SeriesName);
            Assert.Equal("10", second.Number);
            Assert.Equal("2011", second.Volume);
            Assert.Equal("2013", second.Year);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    #endregion

    #region GetMostCommonPerson

    [Fact]
    public void GetMostCommonPerson_ReturnsMostFrequent()
    {
        var personA = new Person
        {
            Id = 1,
            Name = "Publisher A",
            NormalizedName = "publisher a",
            Description = string.Empty,
            PrimaryColor = string.Empty,
            SecondaryColor = string.Empty,
        };
        var personB = new Person
        {
            Id = 2,
            Name = "Publisher B",
            NormalizedName = "publisher b",
            Description = string.Empty,
            PrimaryColor = string.Empty,
            SecondaryColor = string.Empty,
        };

        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Series1", "1", "2020"),
            CreateItem(1, "Series2", "1", "2020"),
            CreateItem(2, "Series3", "1", "2020"),
        };

        // Series1 and Series3 have Publisher A, Series2 has Publisher B
        items[0].Series.Metadata.People = new List<SeriesMetadataPeople>
        {
            new() { Role = PersonRole.Publisher, Person = personA },
        };
        items[1].Series.Metadata.People = new List<SeriesMetadataPeople>
        {
            new() { Role = PersonRole.Publisher, Person = personB },
        };
        items[2].Series.Metadata.People = new List<SeriesMetadataPeople>
        {
            new() { Role = PersonRole.Publisher, Person = personA },
        };

        var result = CblExportService.GetMostCommonPerson(items, PersonRole.Publisher);

        Assert.Equal("Publisher A", result);
    }

    [Fact]
    public void GetMostCommonPerson_NoPeople_ReturnsNull()
    {
        var items = new List<ReadingListItem>
        {
            CreateItem(0, "Series1", "1", "2020"),
        };

        var result = CblExportService.GetMostCommonPerson(items, PersonRole.Publisher);

        Assert.Null(result);
    }

    #endregion
}
