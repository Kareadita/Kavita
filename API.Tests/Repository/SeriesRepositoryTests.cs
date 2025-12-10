using System.Threading.Tasks;
using API.Data;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Helpers.Builders;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Repository;
#nullable enable

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
        var result = await unitOfWork.SeriesRepository.GetPlusSeriesDto(series.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAniListId, result.AniListId);
        Assert.Equal("Test Series", result.SeriesName);
    }

    // TODO: GetSeriesDtoForLibraryIdV2Async Tests (On Deck)




}
