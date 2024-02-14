using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using API.Tests.Helpers;
using EasyCaching.Core;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

internal class MockHostingEnvironment : IHostEnvironment {
    public string ApplicationName { get => "API"; set => throw new NotImplementedException(); }
    public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string ContentRootPath
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public string EnvironmentName { get => "Testing"; set => throw new NotImplementedException(); }
}


public class SeriesServiceTests : AbstractDbTest
{
    private readonly ISeriesService _seriesService;

    public SeriesServiceTests() : base()
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());


        var locService = new LocalizationService(ds, new MockHostingEnvironment(),
            Substitute.For<IMemoryCache>(), Substitute.For<IUnitOfWork>());

        _seriesService = new SeriesService(_unitOfWork, Substitute.For<IEventHub>(),
            Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SeriesService>>(),
            Substitute.For<IScrobblingService>(), locService);
    }
    #region Setup

    protected override async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());
        _context.AppUserRating.RemoveRange(_context.AppUserRating.ToList());
        _context.Genre.RemoveRange(_context.Genre.ToList());
        _context.CollectionTag.RemoveRange(_context.CollectionTag.ToList());
        _context.Person.RemoveRange(_context.Person.ToList());
        _context.Library.RemoveRange(_context.Library.ToList());

        await _context.SaveChangesAsync();
    }

    private static UpdateRelatedSeriesDto CreateRelationsDto(Series series)
    {
        return new UpdateRelatedSeriesDto()
        {
            SeriesId = series.Id,
            Prequels = new List<int>(),
            Adaptations = new List<int>(),
            Characters = new List<int>(),
            Contains = new List<int>(),
            Doujinshis = new List<int>(),
            Others = new List<int>(),
            Sequels = new List<int>(),
            AlternativeSettings = new List<int>(),
            AlternativeVersions = new List<int>(),
            SideStories = new List<int>(),
            SpinOffs = new List<int>(),
            Editions = new List<int>()
        };
    }

    #endregion

    #region SeriesDetail

    [Fact]
    public async Task SeriesDetail_ShouldReturnSpecials()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("Omake").WithIsSpecial(true).WithTitle("Omake").WithPages(1).Build())
                    .WithChapter(new ChapterBuilder("Something SP02").WithIsSpecial(true).WithTitle("Something").WithPages(1).Build())
                    .Build())
                .WithVolume(new VolumeBuilder("2")
                    .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                    .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                    .Build())

                .WithVolume(new VolumeBuilder("3")
                    .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                    .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await _context.SaveChangesAsync();

        var expectedRanges = new[] {"Omake", "Something SP02"};

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Specials);
        Assert.True(2 == detail.Specials.Count());
        Assert.All(detail.Specials, dto => Assert.Contains(dto.Range, expectedRanges));
    }

    [Fact]
    public async Task SeriesDetail_ShouldReturnVolumesAndChapters()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                    .Build())
                .WithVolume(new VolumeBuilder("2")
                    .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                    .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                    .Build())

                .WithVolume(new VolumeBuilder("3")
                    .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                    .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                    .Build())
                .Build())
            .Build()
        );

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Chapters);
        Assert.Equal(6, detail.Chapters.Count());

        Assert.NotEmpty(detail.Volumes);
        Assert.Equal(2, detail.Volumes.Count()); // Volume 0 shouldn't be sent in Volumes
        Assert.All(detail.Volumes, dto => Assert.Contains(dto.Name, new[] {"Volume 2", "Volume 3"})); // Volumes get names mapped
    }

    [Fact]
    public async Task SeriesDetail_ShouldReturnVolumesAndChapters_ButRemove0Chapter()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                    .Build())
                .WithVolume(new VolumeBuilder("2")
                    .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                    .Build())

                .WithVolume(new VolumeBuilder("3")
                    .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Chapters);
        // volume 2 has a 0 chapter aka a single chapter that is represented as a volume. We don't show in Chapters area
        Assert.Equal(3, detail.Chapters.Count());

        Assert.NotEmpty(detail.Volumes);
        Assert.Equal(2, detail.Volumes.Count());
    }

    [Fact]
    public async Task SeriesDetail_ShouldReturnCorrectNaming_VolumeTitle()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")
                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                    .Build())
                .WithVolume(new VolumeBuilder("2")
                    .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                    .Build())

                .WithVolume(new VolumeBuilder("3")
                    .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Chapters);
        // volume 2 has a 0 chapter aka a single chapter that is represented as a volume. We don't show in Chapters area
        Assert.Equal(3, detail.Chapters.Count());

        Assert.NotEmpty(detail.Volumes);
        Assert.Equal(2, detail.Volumes.Count());

        Assert.Equal(string.Empty, detail.Chapters.First().VolumeTitle); // loose leaf chapter
        Assert.Equal("Volume 3", detail.Chapters.Last().VolumeTitle); // volume based chapter
    }

    [Fact]
    public async Task SeriesDetail_ShouldReturnChaptersOnly_WhenBookLibrary()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb", LibraryType.Book)
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("2")
                    .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                    .Build())

                .WithVolume(new VolumeBuilder("3")
                    .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Volumes);

        Assert.Empty(detail.Chapters); // A book library where all books are Volumes, will show no "chapters" on the UI because it doesn't make sense
        Assert.Equal(2, detail.Volumes.Count());
    }

    [Fact]
    public async Task SeriesDetail_WhenBookLibrary_ShouldReturnVolumesAndSpecial()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb", LibraryType.Book)
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("Ano Orokamono ni mo Kyakkou wo! - Volume 1.epub", "Ano Orokamono ni mo Kyakkou wo! - Volume 1.epub").WithIsSpecial(true).WithPages(1).Build())
                    .Build())
                .WithVolume(new VolumeBuilder("2")
                    .WithChapter(new ChapterBuilder("Ano Orokamono ni mo Kyakkou wo! - Volume 2.epub", "Ano Orokamono ni mo Kyakkou wo! - Volume 2.epub").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());



        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Volumes);
        Assert.Equal("2 - Ano Orokamono ni mo Kyakkou wo! - Volume 2", detail.Volumes.ElementAt(0).Name);

        Assert.NotEmpty(detail.Specials);
        Assert.Equal("Ano Orokamono ni mo Kyakkou wo! - Volume 1.epub", detail.Specials.ElementAt(0).Range);

        // A book library where all books are Volumes, will show no "chapters" on the UI because it doesn't make sense
        Assert.Empty(detail.Chapters);

        Assert.Single(detail.Volumes);
    }

    [Fact]
    public async Task SeriesDetail_ShouldSortVolumesByName()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("2")
                    .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                    .Build())

                .WithVolume(new VolumeBuilder("1.2")
                    .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.Equal("Volume 1", detail.Volumes.ElementAt(0).Name);
        Assert.Equal("Volume 1.2", detail.Volumes.ElementAt(1).Name);
        Assert.Equal("Volume 2", detail.Volumes.ElementAt(2).Name);
    }


    #endregion


    #region UpdateRating

    [Fact]
    public async Task UpdateRating_ShouldSetRating()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await _context.SaveChangesAsync();


        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        JobStorage.Current = new InMemoryStorage();
        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 3,
        });

        Assert.True(result);

        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))!
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldUpdateExistingRating()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 3,
        });

        Assert.True(result);

        JobStorage.Current = new InMemoryStorage();
        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);

        // Update the DB again

        var result2 = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 5,
        });

        Assert.True(result2);

        var ratings2 = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings2);
        Assert.True(ratings2.Count == 1);
        Assert.Equal(5, ratings2.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldClampRatingAt5()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 10,
        });

        Assert.True(result);

        JobStorage.Current = new InMemoryStorage();
        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007",
                AppUserIncludes.Ratings)!)
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(5, ratings.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldReturnFalseWhenSeriesDoesntExist()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb", LibraryType.Book)
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 2,
            UserRating = 5,
        });

        Assert.False(result);

        var ratings = user.Ratings;
        Assert.Empty(ratings);
    }

    #endregion

    #region UpdateSeriesMetadata

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldCreateEmptyMetadata_IfDoesntExist()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .Build();
        s.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();

        _context.Series.Add(s);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Genres = new List<GenreTagDto>() {new GenreTagDto() {Id = 0, Title = "New Genre"}}
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series);
        Assert.NotNull(series.Metadata);
        Assert.Contains("New Genre".SentenceCase(), series.Metadata.Genres.Select(g => g.Title));

    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldCreateNewTags_IfNoneExist()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .Build();
        s.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();

        _context.Series.Add(s);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Genres = new List<GenreTagDto>() {new GenreTagDto() {Id = 0, Title = "New Genre"}},
                Tags = new List<TagDto>() {new TagDto() {Id = 0, Title = "New Tag"}},
                Characters = new List<PersonDto>() {new PersonDto() {Id = 0, Name = "Joe Shmo", Role = PersonRole.Character}},
                Colorists = new List<PersonDto>() {new PersonDto() {Id = 0, Name = "Joe Shmo", Role = PersonRole.Colorist}},
                Pencillers = new List<PersonDto>() {new PersonDto() {Id = 0, Name = "Joe Shmo 2", Role = PersonRole.Penciller}},
            },
            CollectionTags = new List<CollectionTagDto>()
            {
                new CollectionTagDto() {Id = 0, Promoted = false, Summary = string.Empty, CoverImageLocked = false, Title = "New Collection"}
            }
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.Contains("New Genre".SentenceCase(), series.Metadata.Genres.Select(g => g.Title));
        Assert.True(series.Metadata.People.All(g => g.Name is "Joe Shmo" or "Joe Shmo 2"));
        Assert.Contains("New Tag".SentenceCase(), series.Metadata.Tags.Select(g => g.Title));
        Assert.Contains("New Collection", series.Metadata.CollectionTags.Select(g => g.Title));

    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldRemoveExistingTags()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        s.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();

        var g = new GenreBuilder("Existing Genre").Build();
        s.Metadata.Genres = new List<Genre>() {g};
        _context.Series.Add(s);

        _context.Genre.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Genres = new List<GenreTagDto>() {new () {Id = 0, Title = "New Genre"}},
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.Genres.Select(g1 => g1.Title).All(g2 => g2 == "New Genre".SentenceCase()));
        Assert.False(series.Metadata.GenresLocked); // GenreLocked is false unless the UI Explicitly says it should be locked
    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldAddNewPerson_NoExistingPeople()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        s.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();

        var g = new PersonBuilder("Existing Person", PersonRole.Publisher).Build();
        _context.Series.Add(s);

        _context.Person.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Publishers = new List<PersonDto>() {new () {Id = 0, Name = "Existing Person", Role = PersonRole.Publisher}},
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.People.Select(g => g.Name).All(g => g == "Existing Person"));
        Assert.False(series.Metadata.PublisherLocked); // PublisherLocked is false unless the UI Explicitly says it should be locked
    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldAddNewPerson_ExistingPeople()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        s.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();
        var g = new PersonBuilder("Existing Person", PersonRole.Publisher).Build();
        s.Metadata.People = new List<Person>() {new PersonBuilder("Existing Writer", PersonRole.Writer).Build(),
            new PersonBuilder("Existing Translator", PersonRole.Translator).Build(), new PersonBuilder("Existing Publisher 2", PersonRole.Publisher).Build()};
        _context.Series.Add(s);

        _context.Person.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Publishers = new List<PersonDto>() {new () {Id = 0, Name = "Existing Person", Role = PersonRole.Publisher}},
                PublisherLocked = true
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.People.Select(g => g.Name).All(g => g == "Existing Person"));
        Assert.True(series.Metadata.PublisherLocked);
    }


    [Fact]
    public async Task UpdateSeriesMetadata_ShouldRemoveExistingPerson()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        s.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();
        var g = new PersonBuilder("Existing Person", PersonRole.Publisher).Build();
        _context.Series.Add(s);

        _context.Person.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Publishers = new List<PersonDto>() {},
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.False(series.Metadata.People.Any());
    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldLockIfTold()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        s.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();
        var g = new GenreBuilder("Existing Genre").Build();
        s.Metadata.Genres = new List<Genre>() {g};
        s.Metadata.GenresLocked = true;
        _context.Series.Add(s);

        _context.Genre.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Genres = new List<GenreTagDto>() {new () {Id = 1, Title = "Existing Genre"}},
                GenresLocked = true
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.Genres.Select(g => g.Title).All(g => g == "Existing Genre".SentenceCase()));
        Assert.True(series.Metadata.GenresLocked);
    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldNotUpdateReleaseYear_IfLessThan1000()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        s.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();
        _context.Series.Add(s);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                ReleaseYear = 100,
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.Equal(0, series.Metadata.ReleaseYear);
        Assert.False(series.Metadata.ReleaseYearLocked);
    }

    #endregion

    #region GetFirstChapterForMetadata

    private static Series CreateSeriesMock()
    {
        var file = new MangaFileBuilder("Test.cbz", MangaFormat.Archive, 1).Build();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).WithFile(file).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).WithFile(file).Build())
                .WithChapter(new ChapterBuilder("A Special Case").WithIsSpecial(true).WithFile(file).WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).WithFile(file).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).WithFile(file).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).WithFile(file).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).WithFile(file).Build())
                .Build())

            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).WithFile(file).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).WithFile(file).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();

        return series;
    }

    [Fact]
    public void GetFirstChapterForMetadata_BookWithOnlyVolumeNumbers_Test()
    {
        var file = new MangaFileBuilder("Test.cbz", MangaFormat.Epub, 1).Build();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).WithFile(file).Build())
                .Build())

            .WithVolume(new VolumeBuilder("1.5")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(2).WithFile(file).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series);
        Assert.NotNull(firstChapter);
        Assert.Equal(1, firstChapter.Pages);
    }

    [Fact]
    public void GetFirstChapterForMetadata_Book_Test()
    {
        var series = CreateSeriesMock();

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series);
        Assert.NotNull(firstChapter);
        Assert.Same("1", firstChapter.Range);
    }

    [Fact]
    public void GetFirstChapterForMetadata_NonBook_ShouldReturnVolume1()
    {
        var series = CreateSeriesMock();

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series);
        Assert.Same("1", firstChapter.Range);
    }

    [Fact]
    public void GetFirstChapterForMetadata_NonBook_ShouldReturnVolume1_WhenFirstChapterIsFloat()
    {
        var series = CreateSeriesMock();
        var files = new List<MangaFile>()
        {
            new MangaFileBuilder("Test.cbz", MangaFormat.Archive, 1).Build()
        };
        series.Volumes[1].Chapters = new List<Chapter>()
        {
            new ChapterBuilder("2").WithFiles(files).WithPages(1).Build(),
            new ChapterBuilder("1.1").WithFiles(files).WithPages(1).Build(),
            new ChapterBuilder("1.2").WithFiles(files).WithPages(1).Build(),
        };

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series);
        Assert.Same("1.1", firstChapter.Range);
    }

    [Fact]
    public void GetFirstChapterForMetadata_NonBook_ShouldReturnChapter1_WhenFirstVolumeIs3()
    {
        var file = new MangaFileBuilder("Test.cbz", MangaFormat.Archive, 1).Build();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").WithPages(1).WithFile(file).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).WithFile(file).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).WithFile(file).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).WithFile(file).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).WithFile(file).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).WithFile(file).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Book).Build();

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series);
        Assert.Same("1", firstChapter.Range);
    }

    #endregion

    #region SeriesRelation
    [Fact]
    public async Task UpdateRelatedSeries_ShouldAddAllRelations()
    {
        await ResetDb();
        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test Series").Build(),
                new SeriesBuilder("Test Series Prequels").Build(),
                new SeriesBuilder("Test Series Sequels").Build(),
            }
        });

        await _context.SaveChangesAsync();

        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related);
        // Add relations
        var addRelationDto = CreateRelationsDto(series1);
        addRelationDto.Adaptations.Add(2);
        addRelationDto.Sequels.Add(3);
        await _seriesService.UpdateRelatedSeries(addRelationDto);
        Assert.Equal(2, series1.Relations.Single(s => s.TargetSeriesId == 2).TargetSeriesId);
        Assert.Equal(3, series1.Relations.Single(s => s.TargetSeriesId == 3).TargetSeriesId);
    }

    [Fact]
    public async Task UpdateRelatedSeries_DeleteAllRelations()
    {
        await ResetDb();
        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test Series").Build(),
                new SeriesBuilder("Test Series Prequels").Build(),
                new SeriesBuilder("Test Series Sequels").Build(),
            }
        });

        await _context.SaveChangesAsync();

        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related);
        // Add relations
        var addRelationDto = CreateRelationsDto(series1);
        addRelationDto.Adaptations.Add(2);
        addRelationDto.Sequels.Add(3);
        await _seriesService.UpdateRelatedSeries(addRelationDto);
        Assert.Equal(2, series1.Relations.Single(s => s.TargetSeriesId == 2).TargetSeriesId);
        Assert.Equal(3, series1.Relations.Single(s => s.TargetSeriesId == 3).TargetSeriesId);

        // Remove relations
        var removeRelationDto = CreateRelationsDto(series1);
        await _seriesService.UpdateRelatedSeries(removeRelationDto);
        Assert.Empty(series1.Relations.Where(s => s.TargetSeriesId == 1));
        Assert.Empty(series1.Relations.Where(s => s.TargetSeriesId == 2));
    }


    [Fact]
    public async Task UpdateRelatedSeries_DeleteTargetSeries_ShouldSucceed()
    {
        await ResetDb();
        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new SeriesBuilder("Series A").Build(),
                new SeriesBuilder("Series B").Build(),
            }
        });

        await _context.SaveChangesAsync();

        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related);
        // Add relations
        var addRelationDto = CreateRelationsDto(series1);
        addRelationDto.Adaptations.Add(2);
        await _seriesService.UpdateRelatedSeries(addRelationDto);
        Assert.Equal(2, series1.Relations.Single(s => s.TargetSeriesId == 2).TargetSeriesId);

        _context.Series.Remove(await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(2));
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            Assert.Fail("Delete of Target Series Failed");
        }

        // Remove relations
        Assert.Empty((await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related)).Relations);
    }

    [Fact]
    public async Task UpdateRelatedSeries_DeleteSourceSeries_ShouldSucceed()
    {
        await ResetDb();
        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new SeriesBuilder("Series A").Build(),
                new SeriesBuilder("Series B").Build(),
            }
        });

        await _context.SaveChangesAsync();

        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related);
        // Add relations
        var addRelationDto = CreateRelationsDto(series1);
        addRelationDto.Adaptations.Add(2);
        await _seriesService.UpdateRelatedSeries(addRelationDto);
        Assert.Equal(2, series1.Relations.Single(s => s.TargetSeriesId == 2).TargetSeriesId);

        _context.Series.Remove(await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1));
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            Assert.Fail("Delete of Target Series Failed");
        }

        // Remove relations
        Assert.Empty((await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(2, SeriesIncludes.Related)).Relations);
    }

    [Fact]
    public async Task UpdateRelatedSeries_ShouldNotAllowDuplicates()
    {
        await ResetDb();
        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test Series").Build(),
                new SeriesBuilder("Test Series Prequels").Build(),
            }
        });

        await _context.SaveChangesAsync();

        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related);
        var relation = new SeriesRelation()
        {
            Series = series1,
            SeriesId = series1.Id,
            TargetSeriesId = 2, // Target series id
            RelationKind = RelationKind.Prequel

        };
        // Manually create a relation
        series1.Relations.Add(relation);

        // Create a new dto with the previous relation as well
        var relationDto = CreateRelationsDto(series1);
        relationDto.Adaptations.Add(2);

        await _seriesService.UpdateRelatedSeries(relationDto);
        // Expected is only one instance of the relation (hence not duping)
        Assert.Equal(2, series1.Relations.Single(s => s.TargetSeriesId == 2).TargetSeriesId);
    }

    [Fact]
    public async Task GetRelatedSeries_EditionPrequelSequel_ShouldNotHaveParent()
    {
        await ResetDb();
        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test Series").Build(),
                new SeriesBuilder("Test Series Editions").Build(),
                new SeriesBuilder("Test Series Prequels").Build(),
                new SeriesBuilder("Test Series Sequels").Build(),
                new SeriesBuilder("Test Series Adaption").Build(),
            }
        });
        await _context.SaveChangesAsync();
        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related);
        // Add relations
        var addRelationDto = CreateRelationsDto(series1);
        addRelationDto.Editions.Add(2);
        addRelationDto.Prequels.Add(3);
        addRelationDto.Sequels.Add(4);
        addRelationDto.Adaptations.Add(5);
        await _seriesService.UpdateRelatedSeries(addRelationDto);


        Assert.Empty((await _seriesService.GetRelatedSeries(1, 2)).Parent);
        Assert.Empty((await _seriesService.GetRelatedSeries(1, 3)).Parent);
        Assert.Empty((await _seriesService.GetRelatedSeries(1, 4)).Parent);
        Assert.NotEmpty((await _seriesService.GetRelatedSeries(1, 5)).Parent);
    }

    [Fact]
    public async Task SeriesRelation_ShouldAllowDeleteOnLibrary()
    {
        await ResetDb();
        var lib = new LibraryBuilder("Test LIb")
            .WithSeries(new SeriesBuilder("Test Series").Build())
            .WithSeries(new SeriesBuilder("Test Series Prequels").Build())
            .WithSeries(new SeriesBuilder("Test Series Sequels").Build())
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .Build();
        _context.Library.Add(lib);

        await _context.SaveChangesAsync();

        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related);
        // Add relations
        var addRelationDto = CreateRelationsDto(series1);
        addRelationDto.Adaptations.Add(2);
        addRelationDto.Sequels.Add(3);
        await _seriesService.UpdateRelatedSeries(addRelationDto);

        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(lib.Id);
        _unitOfWork.LibraryRepository.Delete(library);

        try
        {
            await _unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            Assert.False(true);
        }

        Assert.Null(await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1));
    }

    [Fact]
    public async Task SeriesRelation_ShouldAllowDeleteOnLibrary_WhenSeriesCrossLibraries()
    {
        await ResetDb();
        var lib1 = new LibraryBuilder("Test LIb")
            .WithSeries(new SeriesBuilder("Test Series")
                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1").WithFile(
                        new MangaFileBuilder($"{DataDirectory}1.zip", MangaFormat.Archive)
                            .WithPages(1)
                            .Build()
                        ).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Test Series Prequels").Build())
            .WithSeries(new SeriesBuilder("Test Series Sequels").Build())
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .Build();
        _context.Library.Add(lib1);

        var lib2 = new LibraryBuilder("Test LIb 2", LibraryType.Book)
            .WithSeries(new SeriesBuilder("Test Series 2").Build())
            .WithSeries(new SeriesBuilder("Test Series Prequels 2").Build())
            .WithSeries(new SeriesBuilder("Test Series Prequels 2").Build())// TODO: Is this a bug
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .Build();
        _context.Library.Add(lib2);

        await _context.SaveChangesAsync();

        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Related);
        // Add relations
        var addRelationDto = CreateRelationsDto(series1);
        addRelationDto.Adaptations.Add(4); // cross library link
        await _seriesService.UpdateRelatedSeries(addRelationDto);

        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(lib1.Id, LibraryIncludes.Series);
        _unitOfWork.LibraryRepository.Delete(library);

        try
        {
            await _unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            Assert.False(true);
        }

        Assert.Null(await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1));
    }

    #endregion

    #region UpdateRelatedList

    // TODO: Implement UpdateRelatedList

    #endregion

    #region FormatChapterName

    [Theory]
    [InlineData(LibraryType.Manga, false, "Chapter")]
    [InlineData(LibraryType.Comic, false, "Issue")]
    [InlineData(LibraryType.Comic, true, "Issue #")]
    [InlineData(LibraryType.Book, false, "Book")]
    public async Task FormatChapterNameTest(LibraryType libraryType, bool withHash, string expected )
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();

        Assert.Equal(expected, await _seriesService.FormatChapterName(1, libraryType, withHash));
    }

    #endregion

    #region FormatChapterTitle

    [Fact]
    public async Task FormatChapterTitle_Manga_NonSpecial()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();

        var chapter = new ChapterBuilder("1").WithTitle("Some title").WithIsSpecial(false).Build();
        Assert.Equal("Chapter Some title", await _seriesService.FormatChapterTitle(1, chapter, LibraryType.Manga, false));
    }

    [Fact]
    public async Task FormatChapterTitle_Manga_Special()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();
        var chapter = new ChapterBuilder("1").WithTitle("Some title").WithIsSpecial(true).Build();
        Assert.Equal("Some title", await _seriesService.FormatChapterTitle(1, chapter, LibraryType.Manga, false));
    }

    [Fact]
    public async Task FormatChapterTitle_Comic_NonSpecial_WithoutHash()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();
        var chapter = new ChapterBuilder("1").WithTitle("Some title").WithIsSpecial(false).Build();
        Assert.Equal("Issue Some title", await _seriesService.FormatChapterTitle(1, chapter, LibraryType.Comic, false));
    }

    [Fact]
    public async Task FormatChapterTitle_Comic_Special_WithoutHash()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();
        var chapter = new ChapterBuilder("1").WithTitle("Some title").WithIsSpecial(true).Build();
        Assert.Equal("Some title", await _seriesService.FormatChapterTitle(1, chapter, LibraryType.Comic, false));
    }

    [Fact]
    public async Task FormatChapterTitle_Comic_NonSpecial_WithHash()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();
        var chapter = new ChapterBuilder("1").WithTitle("Some title").WithIsSpecial(false).Build();
        Assert.Equal("Issue #Some title", await _seriesService.FormatChapterTitle(1, chapter, LibraryType.Comic, true));
    }

    [Fact]
    public async Task FormatChapterTitle_Comic_Special_WithHash()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();
        var chapter = new ChapterBuilder("1").WithTitle("Some title").WithIsSpecial(true).Build();
        Assert.Equal("Some title", await _seriesService.FormatChapterTitle(1, chapter, LibraryType.Comic, true));
    }

    [Fact]
    public async Task FormatChapterTitle_Book_NonSpecial()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();
        var chapter = new ChapterBuilder("1").WithTitle("Some title").WithIsSpecial(false).Build();
        Assert.Equal("Book Some title", await _seriesService.FormatChapterTitle(1, chapter, LibraryType.Book, false));
    }

    [Fact]
    public async Task FormatChapterTitle_Book_Special()
    {
        await ResetDb();

        _context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty)
                .WithLocale("en")
                .Build())
            .Build());

        await _context.SaveChangesAsync();
        var chapter = new ChapterBuilder("1").WithTitle("Some title").WithIsSpecial(true).Build();
        Assert.Equal("Some title", await _seriesService.FormatChapterTitle(1, chapter, LibraryType.Book, false));
    }

    #endregion

    #region DeleteMultipleSeries

    [Fact]
    public async Task DeleteMultipleSeries_ShouldDeleteSeries()
    {
        await ResetDb();
        var lib1 = new LibraryBuilder("Test LIb")
            .WithSeries(new SeriesBuilder("Test Series")
                .WithMetadata(new SeriesMetadata()
                {
                    AgeRating = AgeRating.Everyone
                })
                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1").WithFile(
                        new MangaFileBuilder($"{DataDirectory}1.zip", MangaFormat.Archive)
                            .WithPages(1)
                            .Build()
                    ).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Test Series Prequels").Build())
            .WithSeries(new SeriesBuilder("Test Series Sequels").Build())
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .Build();
        _context.Library.Add(lib1);

        var lib2 = new LibraryBuilder("Test LIb 2", LibraryType.Book)
            .WithSeries(new SeriesBuilder("Test Series 2").Build())
            .WithSeries(new SeriesBuilder("Test Series Prequels 2").Build())
            .WithSeries(new SeriesBuilder("Test Series Prequels 2").Build())// TODO: Is this a bug
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .Build();
        _context.Library.Add(lib2);

        await _context.SaveChangesAsync();

        var series1 = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1,
            SeriesIncludes.Related | SeriesIncludes.ExternalRatings);
        // Add relations
        var addRelationDto = CreateRelationsDto(series1);
        addRelationDto.Adaptations.Add(4); // cross library link
        await _seriesService.UpdateRelatedSeries(addRelationDto);


        // Setup External Metadata stuff
        series1.ExternalSeriesMetadata ??= new ExternalSeriesMetadata();
        series1.ExternalSeriesMetadata.ExternalRatings = new List<ExternalRating>()
        {
            new ExternalRating()
            {
                SeriesId = 1,
                Provider = ScrobbleProvider.Mal,
                AverageScore = 1
            }
        };
        series1.ExternalSeriesMetadata.ExternalRecommendations = new List<ExternalRecommendation>()
        {
            new ExternalRecommendation()
            {
                SeriesId = 2,
                Name = "Series 2",
                Url = "",
                CoverUrl = ""
            },
            new ExternalRecommendation()
            {
                SeriesId = 0, // Causes a FK constraint
                Name = "Series 2",
                Url = "",
                CoverUrl = ""
            }
        };
        series1.ExternalSeriesMetadata.ExternalReviews = new List<ExternalReview>()
        {
            new ExternalReview()
            {
                Body = "",
                Provider = ScrobbleProvider.Mal,
                BodyJustText = ""
            }
        };

        await _context.SaveChangesAsync();

        // Ensure we can delete the series
        Assert.True(await _seriesService.DeleteMultipleSeries(new[] {1, 2}));
        Assert.Null(await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1));
        Assert.Null(await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(2));
    }

    #endregion
}
