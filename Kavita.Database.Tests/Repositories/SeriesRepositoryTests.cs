using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Parser;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Database.Tests.Repositories;

public class SeriesRepositoryTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private static async Task SetupSeriesData(IUnitOfWork unitOfWork)
    {
        var library = new LibraryBuilder("GetFullSeriesByAnyName Manga", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder("C:/data/manga/").Build())
            .WithSeries(new SeriesBuilder("The Idaten Deities Know Only Peace")
                .WithLocalizedName("Heion Sedai no Idaten-tachi")
                .WithFormat(MangaFormat.Archive)
                .Build())
            .WithSeries(new SeriesBuilder("Hitomi-chan is Shy With Strangers")
                .WithLocalizedName("Hitomi-chan wa Hitomishiri")
                .WithFormat(MangaFormat.Archive)
                .Build())
            .Build();

        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();
    }


    [Theory]
    [InlineData("The Idaten Deities Know Only Peace", MangaFormat.Archive, "", "The Idaten Deities Know Only Peace")] // Matching on series name in DB
    [InlineData("Heion Sedai no Idaten-tachi", MangaFormat.Archive, "The Idaten Deities Know Only Peace", "The Idaten Deities Know Only Peace")] // Matching on localized name in DB
    [InlineData("Heion Sedai no Idaten-tachi", MangaFormat.Pdf, "", null)]
    [InlineData("Hitomi-chan wa Hitomishiri", MangaFormat.Archive, "", "Hitomi-chan is Shy With Strangers")]
    public async Task GetFullSeriesByAnyName_Should(string seriesName, MangaFormat format, string localizedName, string? expected)
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        await SetupSeriesData(unitOfWork);

        var series =
            await unitOfWork.SeriesRepository.GetFullSeriesByAnyName(seriesName, localizedName,
                2, format, false);
        if (expected == null)
        {
            Assert.Null(series);
        }
        else
        {
            Assert.NotNull(series);
            Assert.Equal(expected, series.Name);
        }
    }

    [Theory]
    // Case 1: Prioritize existing ExternalSeries id
    [InlineData(12345, null, 12345)]
    // Case 2: Extract from weblink if no external series id
    [InlineData(0, "https://anilist.co/manga/100664/Ijiranaide-Nagatorosan/", 100664)]
    // Case 3: Return null if neither exist
    [InlineData(0, "", null)]
    public async Task GetPlusSeriesDto_Should_PrioritizeAniListId_Correctly(int externalAniListId, string? webLinks, int? expectedAniListId)
    {

        var (unitOfWork, _, _) = await CreateDatabase();
        await SetupSeriesData(unitOfWork);

        var series = new SeriesBuilder("Test Series")
            .WithFormat(MangaFormat.Archive)
            .Build();

        var library = new LibraryBuilder("Test Library", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder("C:/data/manga/").Build())
            .WithSeries(series)
            .Build();



        // Set up ExternalSeriesMetadata
        series.ExternalSeriesMetadata = new ExternalSeriesMetadata()
        {
            AniListId = externalAniListId,
            CbrId = 0,
            MalId = 0,
            GoogleBooksId = string.Empty
        };

        series.AniListId = externalAniListId;

        // Set up SeriesMetadata with WebLinks
        series.Metadata = new SeriesMetadata()
        {
            WebLinks = webLinks,
            ReleaseYear = 2021
        };

        unitOfWork.LibraryRepository.Add(library);
        unitOfWork.SeriesRepository.Add(series);
        await unitOfWork.CommitAsync();

        // Act
        var result = await unitOfWork.SeriesRepository.GetKavitaPlusSeriesDetailRequestV3Dto(series.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAniListId, result.AniListId);
        Assert.Equal("Test Series", result.SeriesName);
    }

    // TODO: GetSeriesDtoForLibraryIdV2Async Tests (On Deck)


    #region RemoveSeriesNotInListAsync

    private static ParsedSeries ParsedKey(string folderName, MangaFormat format = MangaFormat.Archive)
    {
        return new ParsedSeries
        {
            Name = folderName,
            NormalizedName = folderName.ToNormalized(),
            Format = format
        };
    }

    /// <summary>
    /// Regression pin for the rename-then-full-scan bug: a series whose Name (and NormalizedName)
    /// was changed but whose OriginalName still matches the on-disk folder must be retained by the
    /// scanner cleanup, not deleted.
    /// </summary>
    [Fact]
    public async Task RemoveSeriesNotInListAsync_RetainsRenamedSeries_ViaOriginalName()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var series = new SeriesBuilder("Batman").WithFormat(MangaFormat.Archive).Build();
        var library = new LibraryBuilder("Removal Test", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("C:/data/comics/").Build())
            .WithSeries(series)
            .Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        // Simulate a UI/K+ rename: Name/NormalizedName move off the folder name, OriginalName stays.
        series.Name = "The Dark Knight";
        series.NormalizedName = "The Dark Knight".ToNormalized();
        unitOfWork.SeriesRepository.Update(series);
        await unitOfWork.CommitAsync();

        // The folder on disk still parses as "Batman"
        var seen = new List<ParsedSeries> { ParsedKey("Batman") };
        var removed = await unitOfWork.SeriesRepository.RemoveSeriesNotInListAsync(seen, library.Id);

        Assert.Empty(removed);
    }

    /// <summary>
    /// Old behavior preserved: a series whose folder no longer exists (no parsed key matches by any
    /// name) is still removed.
    /// </summary>
    [Fact]
    public async Task RemoveSeriesNotInListAsync_RemovesSeriesNoLongerOnDisk()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var batman = new SeriesBuilder("Batman").WithFormat(MangaFormat.Archive).Build();
        var superman = new SeriesBuilder("Superman").WithFormat(MangaFormat.Archive).Build();
        var library = new LibraryBuilder("Removal Test", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("C:/data/comics/").Build())
            .WithSeries(batman)
            .WithSeries(superman)
            .Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        // Only Batman is still on disk
        var seen = new List<ParsedSeries> { ParsedKey("Batman") };
        var removed = await unitOfWork.SeriesRepository.RemoveSeriesNotInListAsync(seen, library.Id);

        Assert.Single(removed);
        Assert.Equal("Superman", removed.First().Name);
    }

    [Fact]
    public async Task RemoveSeriesNotInListAsync_RetainsSeries_ViaLocalizedName()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var series = new SeriesBuilder("My Dress-Up Darling")
            .WithLocalizedName("Sono Bisque Doll wa Koi wo Suru")
            .WithFormat(MangaFormat.Archive)
            .Build();
        var library = new LibraryBuilder("Removal Test", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder("C:/data/manga/").Build())
            .WithSeries(series)
            .Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        // Folder parses to the localized name
        var seen = new List<ParsedSeries> { ParsedKey("Sono Bisque Doll wa Koi wo Suru") };
        var removed = await unitOfWork.SeriesRepository.RemoveSeriesNotInListAsync(seen, library.Id);

        Assert.Empty(removed);
    }

    /// <summary>
    /// Format still discriminates identity: a same-named parsed key of a different format does not
    /// keep an Archive series alive.
    /// </summary>
    [Fact]
    public async Task RemoveSeriesNotInListAsync_FormatDiscriminates()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var series = new SeriesBuilder("Batman").WithFormat(MangaFormat.Archive).Build();
        var library = new LibraryBuilder("Removal Test", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("C:/data/comics/").Build())
            .WithSeries(series)
            .Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var seen = new List<ParsedSeries> { ParsedKey("Batman", MangaFormat.Pdf) };
        var removed = await unitOfWork.SeriesRepository.RemoveSeriesNotInListAsync(seen, library.Id);

        Assert.Single(removed);
        Assert.Equal("Batman", removed.First().Name);
    }

    #endregion
}
