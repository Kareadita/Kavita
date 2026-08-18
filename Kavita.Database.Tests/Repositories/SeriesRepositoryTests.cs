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
    // Collides with another series' Name
    [InlineData("The Idaten Deities Know Only Peace", MangaFormat.Archive, false)]
    // Collides with another series' LocalizedName
    [InlineData("Heion Sedai no Idaten-tachi", MangaFormat.Archive, false)]
    // Same name, different format - no collision
    [InlineData("The Idaten Deities Know Only Peace", MangaFormat.Pdf, true)]
    // Genuinely unique
    [InlineData("A Completely Unique Series Name", MangaFormat.Archive, true)]
    public async Task IsSeriesNameUniqueInLibraryAsync_ChecksAllNameColumns(string candidate, MangaFormat format, bool expectedUnique)
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        await SetupSeriesData(unitOfWork);

        var isUnique = await unitOfWork.SeriesRepository.IsSeriesNameUniqueInLibraryAsync(
            2, format, candidate.ToNormalized(), 0);

        Assert.Equal(expectedUnique, isUnique);
    }

    [Fact]
    public async Task IsSeriesNameUniqueInLibraryAsync_ExcludesSelf()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        await SetupSeriesData(unitOfWork);

        var series = await unitOfWork.SeriesRepository.GetFullSeriesByAnyName(
            "The Idaten Deities Know Only Peace", "", 2, MangaFormat.Archive, false);
        Assert.NotNull(series);

        // A series never collides with itself
        var isUnique = await unitOfWork.SeriesRepository.IsSeriesNameUniqueInLibraryAsync(
            2, MangaFormat.Archive, series.NormalizedName, series.Id);

        Assert.True(isUnique);
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
