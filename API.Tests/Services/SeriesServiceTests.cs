﻿using System;
using System.Collections.Generic;
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
using API.SignalR;
using API.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class SeriesServiceTests : AbstractDbTest
{
    private readonly ISeriesService _seriesService;

    public SeriesServiceTests() : base()
    {
        _seriesService = new SeriesService(_unitOfWork, Substitute.For<IEventHub>(),
            Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SeriesService>>());
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
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("0")
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
                    .Build(),
            }
        });


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
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("0")
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
                    .Build(),
            }
        });

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
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("0")
                        .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                        .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                        .Build())
                    .WithVolume(new VolumeBuilder("2")
                        .WithChapter(new ChapterBuilder("0").WithPages(1).Build())
                        .Build())

                    .WithVolume(new VolumeBuilder("3")
                        .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });

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
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("0")
                        .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                        .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                        .Build())
                    .WithVolume(new VolumeBuilder("2")
                        .WithChapter(new ChapterBuilder("0").WithPages(1).Build())
                        .Build())

                    .WithVolume(new VolumeBuilder("3")
                        .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });

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
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("2")
                        .WithChapter(new ChapterBuilder("0").WithPages(1).Build())
                        .Build())

                    .WithVolume(new VolumeBuilder("3")
                        .WithChapter(new ChapterBuilder("0").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });


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
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("0")
                        .WithChapter(new ChapterBuilder("Ano Orokamono ni mo Kyakkou wo! - Volume 1.epub", "Ano Orokamono ni mo Kyakkou wo! - Volume 1.epub").WithIsSpecial(true).WithPages(1).Build())
                        .Build())
                    .WithVolume(new VolumeBuilder("2")
                        .WithChapter(new ChapterBuilder("Ano Orokamono ni mo Kyakkou wo! - Volume 2.epub", "Ano Orokamono ni mo Kyakkou wo! - Volume 2.epub").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });



        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Volumes);
        Assert.Equal("2 - Ano Orokamono ni mo Kyakkou wo! - Volume 2", detail.Volumes.ElementAt(0).Name);

        Assert.NotEmpty(detail.Specials);
        Assert.Equal("Ano Orokamono ni mo Kyakkou wo! - Volume 1.epub", detail.Specials.ElementAt(0).Range);

        // A book library where all books are Volumes, will show no "chapters" on the UI because it doesn't make sense
        Assert.Empty(detail.Chapters);

        Assert.Equal(1, detail.Volumes.Count());
    }

    [Fact]
    public async Task SeriesDetail_ShouldSortVolumesByName()
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
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("2")
                        .WithChapter(new ChapterBuilder("0").WithPages(1).Build())
                        .Build())

                    .WithVolume(new VolumeBuilder("1.2")
                        .WithChapter(new ChapterBuilder("0").WithPages(1).Build())
                        .Build())
                    .WithVolume(new VolumeBuilder("1")
                        .WithChapter(new ChapterBuilder("0").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });


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
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("1")
                        .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });


        await _context.SaveChangesAsync();


        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 3,
            UserReview = "Average"
        });

        Assert.True(result);

        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);
        Assert.Equal("Average", ratings.First().Review);
    }

    [Fact]
    public async Task UpdateRating_ShouldUpdateExistingRating()
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
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("1")
                        .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });


        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 3,
            UserReview = "Average"
        });

        Assert.True(result);

        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);
        Assert.Equal("Average", ratings.First().Review);

        // Update the DB again

        var result2 = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 5,
            UserReview = "Average"
        });

        Assert.True(result2);

        var ratings2 = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings2);
        Assert.True(ratings2.Count == 1);
        Assert.Equal(5, ratings2.First().Rating);
        Assert.Equal("Average", ratings2.First().Review);
    }

    [Fact]
    public async Task UpdateRating_ShouldClampRatingAt5()
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
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("1")
                        .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 10,
            UserReview = "Average"
        });

        Assert.True(result);

        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(5, ratings.First().Rating);
        Assert.Equal("Average", ratings.First().Review);
    }

    [Fact]
    public async Task UpdateRating_ShouldReturnFalseWhenSeriesDoesntExist()
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
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new SeriesBuilder("Test")
                    .WithMetadata(new SeriesMetadata())
                    .WithVolume(new VolumeBuilder("1")
                        .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                        .Build())
                    .Build(),
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 2,
            UserRating = 5,
            UserReview = "Average"
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
            .WithMetadata(new SeriesMetadata())
            .Build();
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Book,
        };

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
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.Genres.Select(g => g.Title).Contains("New Genre".SentenceCase()));

    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldCreateNewTags_IfNoneExist()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadata())
            .Build();
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Book,
        };

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
        Assert.True(series.Metadata.Genres.Select(g => g.Title).Contains("New Genre".SentenceCase()));
        Assert.True(series.Metadata.People.All(g => g.Name is "Joe Shmo" or "Joe Shmo 2"));
        Assert.True(series.Metadata.Tags.Select(g => g.Title).Contains("New Tag".SentenceCase()));
        Assert.True(series.Metadata.CollectionTags.Select(g => g.Title).Contains("New Collection"));

    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldRemoveExistingTags()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Book,
        };

        var g = DbFactory.Genre("Existing Genre");
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
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Book,
        };

        var g = DbFactory.Person("Existing Person", PersonRole.Publisher);
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
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Book,
        };
        var g = DbFactory.Person("Existing Person", PersonRole.Publisher);
        s.Metadata.People = new List<Person>() {DbFactory.Person("Existing Writer", PersonRole.Writer),
            DbFactory.Person("Existing Translator", PersonRole.Translator), DbFactory.Person("Existing Publisher 2", PersonRole.Publisher)};
        _context.Series.Add(s);

        _context.Person.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Publishers = new List<PersonDto>() {new () {Id = 0, Name = "Existing Person", Role = PersonRole.Publisher}},
                PublishersLocked = true
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
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Book,
        };
        var g = DbFactory.Person("Existing Person", PersonRole.Publisher);
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
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Book,
        };
        var g = DbFactory.Genre("Existing Genre");
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
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Book,
        };
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
        var file = EntityFactory.CreateMangaFile("Test.cbz", MangaFormat.Archive, 1);

        var series = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadata())
            .WithVolume(new VolumeBuilder("0")
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
        series.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
        };

        return series;
    }

    [Fact]
    public void GetFirstChapterForMetadata_Book_Test()
    {
        var series = CreateSeriesMock();

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series, true);
        Assert.Same("1", firstChapter.Range);
    }

    [Fact]
    public void GetFirstChapterForMetadata_NonBook_ShouldReturnVolume1()
    {
        var series = CreateSeriesMock();

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series, false);
        Assert.Same("1", firstChapter.Range);
    }

    [Fact]
    public void GetFirstChapterForMetadata_NonBook_ShouldReturnVolume1_WhenFirstChapterIsFloat()
    {
        var series = CreateSeriesMock();
        var files = new List<MangaFile>()
        {
            EntityFactory.CreateMangaFile("Test.cbz", MangaFormat.Archive, 1)
        };
        series.Volumes[1].Chapters = new List<Chapter>()
        {
            EntityFactory.CreateChapter("2", false, files, 1),
            EntityFactory.CreateChapter("1.1", false, files, 1),
            EntityFactory.CreateChapter("1.2", false, files, 1),
        };

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series, false);
        Assert.Same("1.1", firstChapter.Range);
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
                DbFactory.Series("Test Series"),
                DbFactory.Series("Test Series Prequels"),
                DbFactory.Series("Test Series Sequels"),
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
                DbFactory.Series("Series A"),
                DbFactory.Series("Series B"),
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
                DbFactory.Series("Series A"),
                DbFactory.Series("Series B"),
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
                DbFactory.Series("Test Series"),
                DbFactory.Series("Test Series Prequels"),
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
                DbFactory.Series("Test Series"),
                DbFactory.Series("Test Series Editions"),
                DbFactory.Series("Test Series Prequels"),
                DbFactory.Series("Test Series Sequels"),
                DbFactory.Series("Test Series Adaption"),
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


        Assert.Empty(_seriesService.GetRelatedSeries(1, 2).Result.Parent);
        Assert.Empty(_seriesService.GetRelatedSeries(1, 3).Result.Parent);
        Assert.Empty(_seriesService.GetRelatedSeries(1, 4).Result.Parent);
        Assert.NotEmpty(_seriesService.GetRelatedSeries(1, 5).Result.Parent);
    }

    [Fact]
    public async Task SeriesRelation_ShouldAllowDeleteOnLibrary()
    {
        await ResetDb();
        var lib = new Library()
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
                DbFactory.Series("Test Series"),
                DbFactory.Series("Test Series Prequels"),
                DbFactory.Series("Test Series Sequels"),
            }
        };
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
        var lib1 = new Library()
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
                new SeriesBuilder("Test Series")
                    .WithVolume(new VolumeBuilder("0")
                        .WithChapter(new ChapterBuilder("1").WithFile(new MangaFile()
                        {
                            Pages = 1,
                            FilePath = "fake file"
                        }).Build())
                        .Build())
                    .Build(),
                new SeriesBuilder("Test Series Prequels").Build(),
                new SeriesBuilder("Test Series Sequels").Build(),
            }
        };
        _context.Library.Add(lib1);
        var lib2 = new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb 2",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                DbFactory.Series("Test Series 2"),
                DbFactory.Series("Test Series Prequels 2"),
                DbFactory.Series("Test Series Prequels 2"),
            }
        };
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



    #endregion
}
