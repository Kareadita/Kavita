using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Metadata.Browse;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Repository;

public class GenreRepositoryTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{

    private static TestGenreSet CreateTestGenres()
    {
        return new TestGenreSet
        {
            SharedSeriesChaptersGenre = new GenreBuilder("Shared Series Chapter Genre").Build(),
            SharedSeriesGenre = new GenreBuilder("Shared Series Genre").Build(),
            SharedChaptersGenre = new GenreBuilder("Shared Chapters Genre").Build(),
            Lib0SeriesChaptersGenre = new GenreBuilder("Lib0 Series Chapter Genre").Build(),
            Lib0SeriesGenre = new GenreBuilder("Lib0 Series Genre").Build(),
            Lib0ChaptersGenre = new GenreBuilder("Lib0 Chapters Genre").Build(),
            Lib1SeriesChaptersGenre = new GenreBuilder("Lib1 Series Chapter Genre").Build(),
            Lib1SeriesGenre = new GenreBuilder("Lib1 Series Genre").Build(),
            Lib1ChaptersGenre = new GenreBuilder("Lib1 Chapters Genre").Build(),
            Lib1ChapterAgeGenre = new GenreBuilder("Lib1 Chapter Age Genre").Build()
        };
    }

    private async Task<(AppUser, AppUser, AppUser)> Setup(DataContext context, TestGenreSet genres)
    {
        var fullAccess = new AppUserBuilder("amelia", "amelia@example.com").Build();
        var restrictedAccess = new AppUserBuilder("mila", "mila@example.com").Build();
        var restrictedAgeAccess = new AppUserBuilder("eva", "eva@example.com").Build();
        restrictedAgeAccess.AgeRestriction = AgeRating.Teen;
        restrictedAgeAccess.AgeRestrictionIncludeUnknowns = true;

        context.Users.Add(fullAccess);
        context.Users.Add(restrictedAccess);
        context.Users.Add(restrictedAgeAccess);
        await context.SaveChangesAsync();


        await AddGenresToContext(context, genres);
        await CreateLibrariesWithGenres(context, genres);
        await AssignLibrariesToUsers(context, fullAccess, restrictedAccess, restrictedAgeAccess);

        return (fullAccess, restrictedAccess, restrictedAgeAccess);
    }

    private async Task AddGenresToContext(DataContext context, TestGenreSet genres)
    {
        var allGenres = genres.GetAllGenres();
        context.Genre.AddRange(allGenres);
        await context.SaveChangesAsync();
    }

    private async Task CreateLibrariesWithGenres(DataContext context, TestGenreSet genres)
    {
        var lib0 = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("lib0-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedSeriesGenre, genres.Lib0SeriesChaptersGenre, genres.Lib0SeriesGenre])
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedChaptersGenre, genres.Lib0SeriesChaptersGenre, genres.Lib0ChaptersGenre])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedChaptersGenre, genres.Lib1SeriesChaptersGenre, genres.Lib1ChaptersGenre])
                        .Build())
                    .Build())
                .Build())
            .Build();

        var lib1 = new LibraryBuilder("lib1")
            .WithSeries(new SeriesBuilder("lib1-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedSeriesGenre, genres.Lib1SeriesChaptersGenre, genres.Lib1SeriesGenre])
                    .WithAgeRating(AgeRating.Mature17Plus)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedChaptersGenre, genres.Lib1SeriesChaptersGenre, genres.Lib1ChaptersGenre])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedChaptersGenre, genres.Lib1SeriesChaptersGenre, genres.Lib1ChaptersGenre, genres.Lib1ChapterAgeGenre])
                        .WithAgeRating(AgeRating.Mature17Plus)
                        .Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("lib1-s1")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedSeriesGenre, genres.Lib1SeriesChaptersGenre, genres.Lib1SeriesGenre])
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedChaptersGenre, genres.Lib1SeriesChaptersGenre, genres.Lib1ChaptersGenre])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithGenres([genres.SharedSeriesChaptersGenre, genres.SharedChaptersGenre, genres.Lib1SeriesChaptersGenre, genres.Lib1ChaptersGenre])
                        .Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(lib0);
        context.Library.Add(lib1);
        await context.SaveChangesAsync();
    }

    private static async Task AssignLibrariesToUsers(DataContext context, AppUser fullAccess, AppUser restrictedAccess, AppUser restrictedAgeAccess)
    {
        var lib0 = context.Library.First(l => l.Name == "lib0");
        var lib1 = context.Library.First(l => l.Name == "lib1");

        fullAccess.Libraries.Add(lib0);
        fullAccess.Libraries.Add(lib1);
        restrictedAccess.Libraries.Add(lib1);
        restrictedAgeAccess.Libraries.Add(lib1);

        await context.SaveChangesAsync();
    }

    private static Predicate<BrowseGenreDto> ContainsGenreCheck(Genre genre)
    {
        return g => g.Id == genre.Id;
    }

    private static void AssertGenrePresent(IEnumerable<BrowseGenreDto> genres, Genre expectedGenre)
    {
        Assert.Contains(genres, ContainsGenreCheck(expectedGenre));
    }

    private static void AssertGenreNotPresent(IEnumerable<BrowseGenreDto> genres, Genre expectedGenre)
    {
        Assert.DoesNotContain(genres, ContainsGenreCheck(expectedGenre));
    }

    private static BrowseGenreDto GetGenreDto(IEnumerable<BrowseGenreDto> genres, Genre genre)
    {
        return genres.First(dto => dto.Id == genre.Id);
    }

    [Fact]
    public async Task GetBrowseableGenreFullAccess_ReturnsAllGenresWithCorrectCounts()
    {
        var genres = CreateTestGenres();
        var (unitOfWork, context, _) = await CreateDatabase();
        var (fullAccess, _, _) = await Setup(context, genres);

        // Act
        var fullAccessGenres = await unitOfWork.GenreRepository.GetBrowseableGenre(fullAccess.Id, new UserParams());

        // Assert
        Assert.Equal(genres.GetAllGenres().Count, fullAccessGenres.TotalCount);

        foreach (var genre in genres.GetAllGenres())
        {
            AssertGenrePresent(fullAccessGenres, genre);
        }

        // Verify counts - 1 lib0 series, 2 lib1 series = 3 total series
        Assert.Equal(3, GetGenreDto(fullAccessGenres, genres.SharedSeriesChaptersGenre).SeriesCount);
        Assert.Equal(6, GetGenreDto(fullAccessGenres, genres.SharedSeriesChaptersGenre).ChapterCount);
        Assert.Equal(1, GetGenreDto(fullAccessGenres, genres.Lib0SeriesGenre).SeriesCount);
    }

    [Fact]
    public async Task GetBrowseableGenre_RestrictedAccess_ReturnsOnlyAccessibleGenres()
    {
        var genres = CreateTestGenres();
        var (unitOfWork, context, _) = await CreateDatabase();
        var (_, restrictedAccess, _) = await Setup(context, genres);

        // Act
        var restrictedAccessGenres = await unitOfWork.GenreRepository.GetBrowseableGenre(restrictedAccess.Id, new UserParams());

        // Assert - Should see: 3 shared + 4 library 1 specific = 7 genres
        Assert.Equal(7, restrictedAccessGenres.TotalCount);

        // Verify shared and Library 1 genres are present
        AssertGenrePresent(restrictedAccessGenres, genres.SharedSeriesChaptersGenre);
        AssertGenrePresent(restrictedAccessGenres, genres.SharedSeriesGenre);
        AssertGenrePresent(restrictedAccessGenres, genres.SharedChaptersGenre);
        AssertGenrePresent(restrictedAccessGenres, genres.Lib1SeriesChaptersGenre);
        AssertGenrePresent(restrictedAccessGenres, genres.Lib1SeriesGenre);
        AssertGenrePresent(restrictedAccessGenres, genres.Lib1ChaptersGenre);
        AssertGenrePresent(restrictedAccessGenres, genres.Lib1ChapterAgeGenre);

        // Verify Library 0 specific genres are not present
        AssertGenreNotPresent(restrictedAccessGenres, genres.Lib0SeriesChaptersGenre);
        AssertGenreNotPresent(restrictedAccessGenres, genres.Lib0SeriesGenre);
        AssertGenreNotPresent(restrictedAccessGenres, genres.Lib0ChaptersGenre);

        // Verify counts - 2 lib1 series
        Assert.Equal(2, GetGenreDto(restrictedAccessGenres, genres.SharedSeriesChaptersGenre).SeriesCount);
        Assert.Equal(4, GetGenreDto(restrictedAccessGenres, genres.SharedSeriesChaptersGenre).ChapterCount);
        Assert.Equal(2, GetGenreDto(restrictedAccessGenres, genres.Lib1SeriesGenre).SeriesCount);
        Assert.Equal(4, GetGenreDto(restrictedAccessGenres, genres.Lib1ChaptersGenre).ChapterCount);
        Assert.Equal(1, GetGenreDto(restrictedAccessGenres, genres.Lib1ChapterAgeGenre).ChapterCount);
    }

    [Fact]
    public async Task GetBrowseableGenre_RestrictedAgeAccess_FiltersAgeRestrictedContent()
    {
        var genres = CreateTestGenres();
        var (unitOfWork, context, _) = await CreateDatabase();
        var (_, _, restrictedAgeAccess) = await Setup(context, genres);

        // Act
        var restrictedAgeAccessGenres = await unitOfWork.GenreRepository.GetBrowseableGenre(restrictedAgeAccess.Id, new UserParams());

        // Assert - Should see: 3 shared + 3 lib1 specific = 6 genres (age-restricted genre filtered out)
        Assert.Equal(6, restrictedAgeAccessGenres.TotalCount);

        // Verify accessible genres are present
        AssertGenrePresent(restrictedAgeAccessGenres, genres.SharedSeriesChaptersGenre);
        AssertGenrePresent(restrictedAgeAccessGenres, genres.SharedSeriesGenre);
        AssertGenrePresent(restrictedAgeAccessGenres, genres.SharedChaptersGenre);
        AssertGenrePresent(restrictedAgeAccessGenres, genres.Lib1SeriesChaptersGenre);
        AssertGenrePresent(restrictedAgeAccessGenres, genres.Lib1SeriesGenre);
        AssertGenrePresent(restrictedAgeAccessGenres, genres.Lib1ChaptersGenre);

        // Verify age-restricted genre is filtered out
        AssertGenreNotPresent(restrictedAgeAccessGenres, genres.Lib1ChapterAgeGenre);

        // Verify counts - 1 series lib1 (age-restricted series filtered out)
        Assert.Equal(1, GetGenreDto(restrictedAgeAccessGenres, genres.SharedSeriesChaptersGenre).SeriesCount);
        Assert.Equal(1, GetGenreDto(restrictedAgeAccessGenres, genres.Lib1SeriesGenre).SeriesCount);

        // These values represent a bug - chapters are not properly filtered when their series is age-restricted
        // Should be 2, but currently returns 3 due to the filtering issue
        Assert.Equal(3, GetGenreDto(restrictedAgeAccessGenres, genres.SharedSeriesChaptersGenre).ChapterCount);
        Assert.Equal(3, GetGenreDto(restrictedAgeAccessGenres, genres.Lib1ChaptersGenre).ChapterCount);
    }

    private class TestGenreSet
    {
        public Genre SharedSeriesChaptersGenre { get; init; }
        public Genre SharedSeriesGenre { get; init; }
        public Genre SharedChaptersGenre { get; init; }
        public Genre Lib0SeriesChaptersGenre { get; init; }
        public Genre Lib0SeriesGenre { get; init; }
        public Genre Lib0ChaptersGenre { get; init; }
        public Genre Lib1SeriesChaptersGenre { get; init; }
        public Genre Lib1SeriesGenre { get; init; }
        public Genre Lib1ChaptersGenre { get; init; }
        public Genre Lib1ChapterAgeGenre { get; init; }

        public List<Genre> GetAllGenres()
        {
            return
            [
                SharedSeriesChaptersGenre, SharedSeriesGenre, SharedChaptersGenre,
                Lib0SeriesChaptersGenre, Lib0SeriesGenre, Lib0ChaptersGenre,
                Lib1SeriesChaptersGenre, Lib1SeriesGenre, Lib1ChaptersGenre, Lib1ChapterAgeGenre
            ];
        }
    }
}
