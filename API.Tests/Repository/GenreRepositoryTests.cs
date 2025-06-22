using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Metadata.Browse;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Repository;

public class GenreRepositoryTests : AbstractDbTest
{
    private static readonly Genre SharedSeriesChaptersGenre = new GenreBuilder("Shared Series Chapter Genre").Build();
    private static readonly Genre SharedSeriesGenre = new GenreBuilder("Shared Series Genre").Build();
    private static readonly Genre SharedChaptersGenre = new GenreBuilder("Shared Chapters Genre").Build();
    private static readonly Genre Lib0SeriesChaptersGenre = new GenreBuilder("Lib0 Series Chapter Genre").Build();
    private static readonly Genre Lib0SeriesGenre = new GenreBuilder("Lib0 Series Genre").Build();
    private static readonly Genre Lib0ChaptersGenre = new GenreBuilder("Lib0 Chapters Genre").Build();
    private static readonly Genre Lib1SeriesChaptersGenre = new GenreBuilder("Lib1 Series Chapter Genre").Build();
    private static readonly Genre Lib1SeriesGenre = new GenreBuilder("Lib1 Series Genre").Build();
    private static readonly Genre Lib1ChaptersGenre = new GenreBuilder("Lib1 Chapters Genre").Build();
    private static readonly Genre Lib1ChapterAgeGenre = new GenreBuilder("Lib1 Chapter Age Genre").Build();

    private static readonly List<Genre> AllGenres =
    [
        SharedSeriesChaptersGenre, SharedSeriesGenre, SharedChaptersGenre,
        Lib0SeriesChaptersGenre, Lib0SeriesGenre, Lib0ChaptersGenre,
        Lib1SeriesChaptersGenre, Lib1SeriesGenre, Lib1ChaptersGenre, Lib1ChapterAgeGenre
    ];

    private AppUser _fullAccess;
    private AppUser _restrictedAccess;
    private AppUser _restrictedAgeAccess;

    protected override async Task ResetDb()
    {
        Context.Genre.RemoveRange(Context.Genre);
        Context.Library.RemoveRange(Context.Library);
        await Context.SaveChangesAsync();
    }

    private async Task SeedDb()
    {
        _fullAccess = new AppUserBuilder("amelia", "amelia@example.com").Build();
        _restrictedAccess = new AppUserBuilder("mila", "mila@example.com").Build();
        _restrictedAgeAccess = new AppUserBuilder("eva", "eva@example.com").Build();
        _restrictedAgeAccess.AgeRestriction = AgeRating.Teen;
        _restrictedAgeAccess.AgeRestrictionIncludeUnknowns = true;

        Context.Users.Add(_fullAccess);
        Context.Users.Add(_restrictedAccess);
        Context.Users.Add(_restrictedAgeAccess);
        await Context.SaveChangesAsync();

        Context.Genre.AddRange(AllGenres);
        await Context.SaveChangesAsync();

        var lib0 = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("lib0-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithGenres([SharedSeriesChaptersGenre, SharedSeriesGenre, Lib0SeriesChaptersGenre, Lib0SeriesGenre])
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithGenres([SharedSeriesChaptersGenre, SharedChaptersGenre, Lib0SeriesChaptersGenre, Lib0ChaptersGenre])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithGenres([SharedSeriesChaptersGenre, SharedChaptersGenre, Lib1SeriesChaptersGenre, Lib1ChaptersGenre])
                        .Build())
                    .Build())
                .Build())
            .Build();

        var lib1 = new LibraryBuilder("lib1")
            .WithSeries(new SeriesBuilder("lib1-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithGenres([SharedSeriesChaptersGenre, SharedSeriesGenre, Lib1SeriesChaptersGenre, Lib1SeriesGenre])
                    .WithAgeRating(AgeRating.Mature17Plus)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithGenres([SharedSeriesChaptersGenre, SharedChaptersGenre, Lib1SeriesChaptersGenre, Lib1ChaptersGenre])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithGenres([SharedSeriesChaptersGenre, SharedChaptersGenre, Lib1SeriesChaptersGenre, Lib1ChaptersGenre, Lib1ChapterAgeGenre])
                        .WithAgeRating(AgeRating.Mature17Plus)
                        .Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("lib1-s1")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithGenres([SharedSeriesChaptersGenre, SharedSeriesGenre, Lib1SeriesChaptersGenre, Lib1SeriesGenre])
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithGenres([SharedSeriesChaptersGenre, SharedChaptersGenre, Lib1SeriesChaptersGenre, Lib1ChaptersGenre])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithGenres([SharedSeriesChaptersGenre, SharedChaptersGenre, Lib1SeriesChaptersGenre, Lib1ChaptersGenre])
                        .Build())
                    .Build())
                .Build())
            .Build();

        await Context.SaveChangesAsync();

        _fullAccess.Libraries.Add(lib0);
        _fullAccess.Libraries.Add(lib1);
        _restrictedAccess.Libraries.Add(lib1);
        _restrictedAgeAccess.Libraries.Add(lib1);

        await Context.SaveChangesAsync();
    }

    private static Predicate<BrowseGenreDto> ContainsGenreCheck(Genre genre)
    {
        return g => g.Id == genre.Id;
    }

    [Fact]
    public async Task GetBrowseableGenre()
    {
        await ResetDb();
        await SeedDb();

        var fullAccessGenres = await UnitOfWork.GenreRepository.GetBrowseableGenre(_fullAccess.Id, new UserParams());
        Assert.Equal(AllGenres.Count, fullAccessGenres.TotalCount);
        foreach (var genre in AllGenres)
        {
            Assert.Contains(fullAccessGenres, ContainsGenreCheck(genre));
        }

        // 1 lib0 series, 2 lib1 series
        Assert.Equal(3, fullAccessGenres.First(dto => dto.Id == SharedSeriesChaptersGenre.Id).SeriesCount);
        Assert.Equal(6, fullAccessGenres.First(dto => dto.Id == SharedSeriesChaptersGenre.Id).ChapterCount);
        Assert.Equal(1, fullAccessGenres.First(dto => dto.Id == Lib0SeriesGenre.Id).SeriesCount);


        var restrictedAccessGenres = await UnitOfWork.GenreRepository.GetBrowseableGenre(_restrictedAccess.Id, new UserParams());

        Assert.Equal(7, restrictedAccessGenres.TotalCount);

        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(SharedSeriesChaptersGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(SharedSeriesGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(SharedChaptersGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(Lib1SeriesChaptersGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(Lib1SeriesGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(Lib1ChaptersGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(Lib1ChapterAgeGenre));

        // 2 lib1 series
        Assert.Equal(2, restrictedAccessGenres.First(dto => dto.Id == SharedSeriesChaptersGenre.Id).SeriesCount);
        Assert.Equal(4, restrictedAccessGenres.First(dto => dto.Id == SharedSeriesChaptersGenre.Id).ChapterCount);
        Assert.Equal(2, restrictedAccessGenres.First(dto => dto.Id == Lib1SeriesGenre.Id).SeriesCount);
        Assert.Equal(4, restrictedAccessGenres.First(dto => dto.Id == Lib1ChaptersGenre.Id).ChapterCount);
        Assert.Equal(1, restrictedAccessGenres.First(dto => dto.Id == Lib1ChapterAgeGenre.Id).ChapterCount);


        var restrictedAgeAccessGenres = await UnitOfWork.GenreRepository.GetBrowseableGenre(_restrictedAgeAccess.Id, new UserParams());

        Assert.Equal(6, restrictedAgeAccessGenres.TotalCount);

        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(SharedSeriesChaptersGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(SharedSeriesGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(SharedChaptersGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(Lib1SeriesChaptersGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(Lib1SeriesGenre));
        Assert.Contains(restrictedAccessGenres, ContainsGenreCheck(Lib1ChaptersGenre));
        Assert.DoesNotContain(restrictedAgeAccessGenres, ContainsGenreCheck(Lib1ChapterAgeGenre));

        Assert.Equal(1, restrictedAgeAccessGenres.First(dto => dto.Id == SharedSeriesChaptersGenre.Id).SeriesCount);
        Assert.Equal(1, restrictedAgeAccessGenres.First(dto => dto.Id == Lib1SeriesGenre.Id).SeriesCount);

        // These values are a "bug". And should be 2, however chapters are not filtered out when their series is
        Assert.Equal(3, restrictedAgeAccessGenres.First(dto => dto.Id == SharedSeriesChaptersGenre.Id).ChapterCount);
        Assert.Equal(3, restrictedAgeAccessGenres.First(dto => dto.Id == Lib1ChaptersGenre.Id).ChapterCount);
    }
}
