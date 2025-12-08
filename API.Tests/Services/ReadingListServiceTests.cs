using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.ReadingLists;
using API.DTOs.ReadingLists.CBL;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Reading;
using API.SignalR;
using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class ReadingListServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    public (IReadingListService, IReaderService) Setup(IUnitOfWork unitOfWork, DataContext context, IMapper mapper)
    {

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem());
        var readingListService = new ReadingListService(unitOfWork, Substitute.For<ILogger<ReadingListService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(), ds);

        var readerService = new ReaderService(unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()),
            Substitute.For<IScrobblingService>(), Substitute.For<IReadingSessionService>(), Substitute.For<IClientInfoAccessor>());

        return (readingListService, readerService);
    }

    #region AddChaptersToReadingList
    [Fact]
    public async Task AddChaptersToReadingList_ShouldAddFirstItem_AsOrderZero()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);

        var library = new LibraryBuilder("Test Lib", LibraryType.Book)
            .WithSeries(new SeriesBuilder("Test")
                .WithMetadata(new SeriesMetadataBuilder().Build())
                .WithVolumes(new List<Volume>()
                {
                    new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                        .WithChapter(new ChapterBuilder("1")
                            .WithAgeRating(AgeRating.Everyone)
                            .Build()
                        )
                        .WithChapter(new ChapterBuilder("2")
                            .WithAgeRating(AgeRating.X18Plus)
                            .Build()
                        )
                        .WithChapter(new ChapterBuilder("3")
                            .WithAgeRating(AgeRating.X18Plus)
                            .Build()
                        )
                        .Build()
                })
                .Build())
            .Build();
        await context.SaveChangesAsync();

        context.AppUser.Add(new AppUserBuilder("majora2007", "")
            .WithLibrary(library)
            .Build()
        );

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        user!.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1}, readingList);
        await unitOfWork.CommitAsync();

        Assert.Single(readingList.Items);
        Assert.Equal(0, readingList.Items.First().Order);
    }

    [Fact]
    public async Task AddChaptersToReadingList_ShouldNewItems_AfterLastOrder()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        context.AppUser.Add(new AppUserBuilder("majora2007", "")
            .WithLibrary(new LibraryBuilder("Test LIb", LibraryType.Book)
                .WithSeries(new SeriesBuilder("Test")
                    .WithVolumes(new List<Volume>()
                    {
                        new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                            .WithChapter(new ChapterBuilder("1")
                                .WithAgeRating(AgeRating.Everyone)
                                .Build()
                            )
                            .WithChapter(new ChapterBuilder("2")
                                .WithAgeRating(AgeRating.X18Plus)
                                .Build()
                            )
                            .WithChapter(new ChapterBuilder("3")
                                .WithAgeRating(AgeRating.X18Plus)
                                .Build()
                            )
                            .Build()
                    })
                    .Build())
                .Build()
            )
            .Build()
        );

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        user!.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1}, readingList);
        await unitOfWork.CommitAsync();
        await readingListService.AddChaptersToReadingList(1, new List<int>() {2}, readingList);
        await unitOfWork.CommitAsync();

        Assert.Equal(2, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.First().Order);
        Assert.Equal(1, readingList.Items.ElementAt(1).Order);
    }
    #endregion

    #region UpdateReadingListItemPosition


    [Fact]
    public async Task UpdateReadingListItemPosition_MoveLastToFirst_TwoItemsShouldShift()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb", LibraryType.Book)
                    .WithSeries(new SeriesBuilder("Test")
                        .WithMetadata(new SeriesMetadataBuilder().Build())
                        .WithVolumes(new List<Volume>()
                        {
                            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                                .WithChapter(new ChapterBuilder("1")
                                    .WithAgeRating(AgeRating.Everyone)
                                    .Build()
                                )
                                .WithChapter(new ChapterBuilder("2")
                                    .WithAgeRating(AgeRating.X18Plus)
                                    .Build()
                                )
                                .WithChapter(new ChapterBuilder("3")
                                    .WithAgeRating(AgeRating.X18Plus)
                                    .Build()
                                )
                                .Build()
                        })
                        .Build())
                    .Build()
            }
        });

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        Assert.NotNull(user);

        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2, 3}, readingList);
        await unitOfWork.CommitAsync();
        Assert.Equal(3, readingList.Items.Count);

        await readingListService.UpdateReadingListItemPosition(new UpdateReadingListPosition()
        {
            FromPosition = 2, ToPosition = 0, ReadingListId = 1, ReadingListItemId = 3
        });


        Assert.Equal(3, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.Single(i => i.ChapterId == 3).Order);
        Assert.Equal(1, readingList.Items.Single(i => i.ChapterId == 1).Order);
        Assert.Equal(2, readingList.Items.Single(i => i.ChapterId == 2).Order);
    }

    [Fact]
    public async Task UpdateReadingListItemPosition_MoveLastToFirst_TwoItemsShouldShift_ThenDeleteSecond_OrderShouldBeCorrect()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb", LibraryType.Book)
                    .WithSeries(new SeriesBuilder("Test")
                        .WithMetadata(new SeriesMetadataBuilder().Build())
                        .WithVolumes(new List<Volume>()
                        {
                            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                                .WithChapter(new ChapterBuilder("1")
                                    .WithAgeRating(AgeRating.Everyone)
                                    .Build()
                                )
                                .WithChapter(new ChapterBuilder("2")
                                    .WithAgeRating(AgeRating.X18Plus)
                                    .Build()
                                )
                                .WithChapter(new ChapterBuilder("3")
                                    .WithAgeRating(AgeRating.X18Plus)
                                    .Build()
                                )
                                .Build()
                        })
                        .Build())
                    .Build()
            }
        });

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        user!.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        // Existing (order, chapterId): (0, 1), (1, 2), (2, 3)
        await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2, 3}, readingList);
        await unitOfWork.CommitAsync();
        Assert.Equal(3, readingList.Items.Count);

        // From 3 to 1
        // New (order, chapterId): (0, 3), (1, 2), (2, 1)
        await readingListService.UpdateReadingListItemPosition(new UpdateReadingListPosition()
        {
            FromPosition = 2, ToPosition = 0, ReadingListId = 1, ReadingListItemId = 3
        });



        Assert.Equal(3, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.Single(i => i.ChapterId == 3).Order);
        Assert.Equal(1, readingList.Items.Single(i => i.ChapterId == 1).Order);
        Assert.Equal(2, readingList.Items.Single(i => i.ChapterId == 2).Order);

        // New (order, chapterId): (0, 3), (2, 1): Delete 2nd item
        await readingListService.DeleteReadingListItem(new UpdateReadingListPosition()
        {
            ReadingListId = 1, ReadingListItemId = readingList.Items.Single(i => i.ChapterId == 2).Id
        });

        Assert.Equal(2, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.Single(i => i.ChapterId == 3).Order);
        Assert.Equal(1, readingList.Items.Single(i => i.ChapterId == 1).Order);
    }


    #endregion

    #region DeleteReadingListItem

    [Fact]
    public async Task DeleteReadingListItem_DeleteFirstItem_SecondShouldBecomeFirst()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb", LibraryType.Book)
                    .WithSeries(new SeriesBuilder("Test")
                        .WithMetadata(new SeriesMetadataBuilder().Build())
                        .WithVolumes(new List<Volume>()
                        {
                            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                                .WithChapter(new ChapterBuilder("1")
                                    .WithAgeRating(AgeRating.Everyone)
                                    .Build()
                                )
                                .WithChapter(new ChapterBuilder("2")
                                    .WithAgeRating(AgeRating.X18Plus)
                                    .Build()
                                )
                                .Build()
                        })
                        .Build())
                    .Build()
            }
        });

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        Assert.NotNull(user);
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);
        await unitOfWork.CommitAsync();
        Assert.Equal(2, readingList.Items.Count);

        await readingListService.DeleteReadingListItem(new UpdateReadingListPosition()
        {
            ReadingListId = 1, ReadingListItemId = 1
        });

        Assert.Single(readingList.Items);
        Assert.Equal(2, readingList.Items.First().ChapterId);
    }

    #endregion

    #region RemoveFullyReadItems

    [Fact]
    public async Task RemoveFullyReadItems_RemovesAllFullyReadItems()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, readerService) = Setup(unitOfWork, context, mapper);
        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb", LibraryType.Book)
                    .WithSeries(new SeriesBuilder("Test")
                        .WithMetadata(new SeriesMetadataBuilder().Build())
                        .WithVolumes(new List<Volume>()
                        {
                            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                                .WithChapter(new ChapterBuilder("1")
                                    .WithAgeRating(AgeRating.Everyone)
                                    .Build()
                                )
                                .WithChapter(new ChapterBuilder("2")
                                    .WithAgeRating(AgeRating.X18Plus)
                                    .Build()
                                )
                                .WithChapter(new ChapterBuilder("3")
                                    .WithAgeRating(AgeRating.X18Plus)
                                    .Build()
                                )
                                .Build()
                        })
                        .Build())
                    .Build()
            }
        });

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists | AppUserIncludes.Progress);
        var readingList = new ReadingListBuilder("test").Build();
        Assert.NotNull(user);
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2, 3}, readingList);
        await unitOfWork.CommitAsync();
        Assert.Equal(3, readingList.Items.Count);

        // Mark 2 as fully read
        await readerService.MarkChaptersAsRead(user, 1,
            (await unitOfWork.ChapterRepository.GetChaptersByIdsAsync(new List<int>() {2})).ToList());
        await unitOfWork.CommitAsync();

        await readingListService.RemoveFullyReadItems(1, user);


        Assert.Equal(2, readingList.Items.Count);
        Assert.DoesNotContain(readingList.Items, i => i.Id == 2);
    }


    #endregion

    #region CalculateAgeRating

    [Fact]
    public async Task CalculateAgeRating_ShouldUpdateToUnknown_IfNoneSet()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb", LibraryType.Book)
                    .WithSeries(new SeriesBuilder("Test")
                        .WithMetadata(new SeriesMetadataBuilder().Build())
                        .WithVolumes(new List<Volume>()
                        {
                            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                                .WithChapter(new ChapterBuilder("1")
                                    .Build()
                                )
                                .WithChapter(new ChapterBuilder("2")
                                    .Build()
                                )
                                .Build()
                        })
                        .Build())
                    .Build()
            }
        });

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        user!.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        await readingListService.CalculateReadingListAgeRating(readingList);
        Assert.Equal(AgeRating.Unknown, readingList.AgeRating);
    }

    [Fact]
    public async Task CalculateAgeRating_ShouldUpdateToMax()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .WithVolumes(new List<Volume>()
            {
                new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1")
                        .Build()
                    )
                    .WithChapter(new ChapterBuilder("2")
                        .Build()
                    )
                    .Build()
            })
            .Build();
        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb 2", LibraryType.Book)
                    .WithSeries(s)
                    .Build()
            }
        });

        s.Metadata.AgeRating = AgeRating.G;

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        Assert.NotNull(user);
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        await readingListService.CalculateReadingListAgeRating(readingList);
        Assert.Equal(AgeRating.G, readingList.AgeRating);
    }

    [Fact]
    public async Task UpdateReadingListAgeRatingForSeries()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var spiceAndWolf = new SeriesBuilder("Spice and Wolf")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .WithVolumes([
            new VolumeBuilder("1")
                .WithChapters([
                    new ChapterBuilder("1").Build(),
                    new ChapterBuilder("2").Build(),
                ]).Build()
            ]).Build();
        spiceAndWolf.Metadata.AgeRating = AgeRating.Everyone;

        var othersidePicnic = new SeriesBuilder("Otherside Picnic ")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .WithVolumes([
                new VolumeBuilder("1")
                    .WithChapters([
                        new ChapterBuilder("1").Build(),
                        new ChapterBuilder("2").Build(),
                    ]).Build()
            ]).Build();
        othersidePicnic.Metadata.AgeRating = AgeRating.Everyone;

        context.AppUser.Add(new AppUser()
        {
            UserName = "Amelia",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>
            {
                new LibraryBuilder("Test Library", LibraryType.LightNovel)
                    .WithSeries(spiceAndWolf)
                    .WithSeries(othersidePicnic)
                    .Build(),
            },
        });

        await context.SaveChangesAsync();
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("Amelia", AppUserIncludes.ReadingLists);
        Assert.NotNull(user);

        var myTestReadingList = new ReadingListBuilder("MyReadingList").Build();
        var mySecondTestReadingList = new ReadingListBuilder("MySecondReadingList").Build();
        var myThirdTestReadingList = new ReadingListBuilder("MyThirdReadingList").Build();
        user.ReadingLists = new List<ReadingList>()
        {
            myTestReadingList,
            mySecondTestReadingList,
            myThirdTestReadingList,
        };


        await readingListService.AddChaptersToReadingList(spiceAndWolf.Id, new List<int> {1, 2}, myTestReadingList);
        await readingListService.AddChaptersToReadingList(othersidePicnic.Id, new List<int> {3, 4}, myTestReadingList);
        await readingListService.AddChaptersToReadingList(spiceAndWolf.Id, new List<int> {1, 2}, myThirdTestReadingList);
        await readingListService.AddChaptersToReadingList(othersidePicnic.Id, new List<int> {3, 4}, mySecondTestReadingList);


        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        await readingListService.CalculateReadingListAgeRating(myTestReadingList);
        await readingListService.CalculateReadingListAgeRating(mySecondTestReadingList);
        Assert.Equal(AgeRating.Everyone, myTestReadingList.AgeRating);
        Assert.Equal(AgeRating.Everyone, mySecondTestReadingList.AgeRating);
        Assert.Equal(AgeRating.Everyone, myThirdTestReadingList.AgeRating);

        await readingListService.UpdateReadingListAgeRatingForSeries(othersidePicnic.Id, AgeRating.Mature);
        await unitOfWork.CommitAsync();

        // Reading lists containing Otherside Picnic are updated
        myTestReadingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(1);
        Assert.NotNull(myTestReadingList);
        Assert.Equal(AgeRating.Mature, myTestReadingList.AgeRating);

        mySecondTestReadingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(2);
        Assert.NotNull(mySecondTestReadingList);
        Assert.Equal(AgeRating.Mature, mySecondTestReadingList.AgeRating);

        // Unrelated reading list is not updated
        myThirdTestReadingList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(3);
        Assert.NotNull(myThirdTestReadingList);
        Assert.Equal(AgeRating.Everyone, myThirdTestReadingList.AgeRating);
    }

    #endregion

    #region CalculateStartAndEndDates

    [Fact]
    public async Task CalculateStartAndEndDates_ShouldBeNothing_IfNothing()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .WithVolumes(new List<Volume>()
            {
                new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1")
                        .Build()
                    )
                    .WithChapter(new ChapterBuilder("2")
                        .Build()
                    )
                    .Build()
            })
            .Build();
        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb 2", LibraryType.Book)
                    .WithSeries(s)
                    .Build()
            }
        });

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        Assert.NotNull(user);
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        await readingListService.CalculateStartAndEndDates(readingList);
        Assert.Equal(0, readingList.StartingMonth);
        Assert.Equal(0, readingList.StartingYear);
        Assert.Equal(0, readingList.EndingMonth);
        Assert.Equal(0, readingList.EndingYear);
    }

    [Fact]
    public async Task CalculateStartAndEndDates_ShouldBeSomething_IfChapterHasSet()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var s = new SeriesBuilder("Test")
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .WithVolumes(new List<Volume>()
            {
                new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1")
                        .WithReleaseDate(new DateTime(2005, 03, 01))
                        .Build()
                    )
                    .WithChapter(new ChapterBuilder("2")
                        .WithReleaseDate(new DateTime(2002, 03, 01))
                        .Build()
                    )
                    .Build()
            })
            .Build();
        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb 2", LibraryType.Book)
                    .WithSeries(s)
                    .Build()
            }
        });

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingListBuilder("test").Build();
        Assert.NotNull(user);
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        await readingListService.CalculateStartAndEndDates(readingList);
        Assert.Equal(3, readingList.StartingMonth);
        Assert.Equal(2002, readingList.StartingYear);
        Assert.Equal(3, readingList.EndingMonth);
        Assert.Equal(2005, readingList.EndingYear);
    }

    #endregion

    #region FormatTitle

    [Fact]
    public void FormatTitle_ShouldFormatCorrectly()
    {
        // Manga Library & Archive
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, "1")));
        Assert.Equal("Chapter 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, "1", "1")));
        Assert.Equal("Chapter 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, "1", "1", "The Title")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, chapterTitleName: "The Title")));

        // Comic Library & Archive
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, "1")));
        Assert.Equal("Issue #1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, "1", "1")));
        Assert.Equal("Issue #1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, "1", "1", "The Title")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, chapterTitleName: "The Title")));
        var dto = CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, chapterNumber: "The Special Title");
        dto.IsSpecial = true;
        Assert.Equal("The Special Title", ReadingListService.FormatTitle(dto));

        // Book Library & Archive
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, "1")));
        Assert.Equal("Book 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, "1", "1")));
        Assert.Equal("Book 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, "1", "1", "The Title")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, chapterTitleName: "The Title")));

        // Manga Library & EPUB
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, "1")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, "1", "1")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, "1", "1", "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, chapterTitleName: "The Title")));

        // Book Library & EPUB
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, "1")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, "1", "1")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, "1", "1", "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, chapterTitleName: "The Title")));

    }

    private static ReadingListItemDto CreateListItemDto(MangaFormat seriesFormat, LibraryType libraryType,
        string volumeNumber = API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume,
        string chapterNumber =API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
        string chapterTitleName = "")
    {
        return new ReadingListItemDto()
        {
            SeriesFormat = seriesFormat,
            LibraryType = libraryType,
            VolumeNumber = volumeNumber,
            ChapterNumber = chapterNumber,
            ChapterTitleName = chapterTitleName
        };
    }

    #endregion

    #region CreateReadingList

    private async Task CreateReadingList_SetupBaseData(IUnitOfWork unitOfWork, DataContext context)
    {

        var fablesSeries = new SeriesBuilder("Fables").Build();
        fablesSeries.Volumes.Add(
            new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithName("2002")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build()
            );

        // NOTE: WithLibrary creates a SideNavStream hence why we need to use the same instance for multiple users to avoid an id conflict
        var library = new LibraryBuilder("Test LIb 2", LibraryType.Book)
            .WithSeries(fablesSeries)
            .Build();

        context.AppUser.Add(new AppUserBuilder("majora2007", string.Empty)
            .WithLibrary(library)
            .Build()
        );
        context.AppUser.Add(new AppUserBuilder("admin", string.Empty)
            .WithLibrary(library)
            .Build()
        );
        await unitOfWork.CommitAsync();
    }

    [Fact]
    public async Task CreateReadingList_ShouldCreate_WhenNoOtherListsOnUser()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        await CreateReadingList_SetupBaseData(unitOfWork, context);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);

        await readingListService.CreateReadingListForUser(user, "Test List");

        user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);
        Assert.NotEmpty(user.ReadingLists);
    }

    [Fact]
    public async Task CreateReadingList_ShouldNotCreate_WhenExistingList()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        await CreateReadingList_SetupBaseData(unitOfWork, context);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);

        await readingListService.CreateReadingListForUser(user, "Test List");
        var afterUser = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(afterUser);
        Assert.Single(afterUser .ReadingLists);
        try
        {
            await readingListService.CreateReadingListForUser(user, "Test List");
        }
        catch (Exception ex)
        {
            Assert.Equal("reading-list-name-exists", ex.Message);
        }

        afterUser = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(afterUser);
        Assert.Single(afterUser .ReadingLists);
    }

    [Fact]
    public async Task CreateReadingList_ShouldNotCreate_WhenPromotedListExists()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        await CreateReadingList_SetupBaseData(unitOfWork, context);


        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("admin", AppUserIncludes.ReadingLists);
        Assert.NotNull(user);

        var list = await readingListService.CreateReadingListForUser(user, "Test List");
        await readingListService.UpdateReadingList(list,
            new UpdateReadingListDto()
            {
                ReadingListId = list.Id, Promoted = true, Title = list.Title, Summary = list.Summary,
                CoverImageLocked = false
            });

        try
        {
            await readingListService.CreateReadingListForUser(user, "Test List");
        }
        catch (Exception ex)
        {
            Assert.Equal("reading-list-name-exists", ex.Message);
        }
    }

    #endregion

    #region UpdateReadingList
    #endregion

    #region DeleteReadingList
    [Fact]
    public async Task DeleteReadingList_ShouldDelete()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        await CreateReadingList_SetupBaseData(unitOfWork, context);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);

        await readingListService.CreateReadingListForUser(user, "Test List");

        user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);
        Assert.NotEmpty(user.ReadingLists);
        try
        {
            await readingListService.CreateReadingListForUser(user, "Test List");
        }
        catch (Exception ex)
        {
            Assert.Equal("reading-list-name-exists", ex.Message);
        }

        user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);
        Assert.Single(user.ReadingLists);

        await readingListService.DeleteReadingList(1, user);
        user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);
        Assert.Empty(user.ReadingLists);
    }
    #endregion

    #region UserHasReadingListAccess
    // TODO: UserHasReadingListAccess tests are unavailable because I can't mock UserManager<AppUser>
    [Fact(Skip = "Unable to mock UserManager<AppUser>")]
    public async Task UserHasReadingListAccess_ShouldWorkIfTheirList()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        await CreateReadingList_SetupBaseData(unitOfWork, context);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);
        await readingListService.CreateReadingListForUser(user, "Test List");

        var userWithList = await readingListService.UserHasReadingListAccess(1, "majora2007");
        Assert.NotNull(userWithList);
        Assert.Single(userWithList.ReadingLists);
    }

    [Fact(Skip = "Unable to mock UserManager<AppUser>")]
    public async Task UserHasReadingListAccess_ShouldNotWork_IfNotTheirList()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        await CreateReadingList_SetupBaseData(unitOfWork, context);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(2, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);
        await readingListService.CreateReadingListForUser(user, "Test List");

        var userWithList = await readingListService.UserHasReadingListAccess(1, "majora2007");
        Assert.Null(userWithList);
    }

    [Fact(Skip = "Unable to mock UserManager<AppUser>")]
    public async Task UserHasReadingListAccess_ShouldWork_IfNotTheirList_ButUserIsAdmin()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        await CreateReadingList_SetupBaseData(unitOfWork, context);


        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);
        await readingListService.CreateReadingListForUser(user, "Test List");

        //var admin = await unitOfWork.UserRepository.GetUserByIdAsync(2, AppUserIncludes.ReadingLists);
        //_userManager.When(x => x.IsInRoleAsync(user, PolicyConstants.AdminRole)).Returns((info => true), null);

        //_userManager.IsInRoleAsync(admin, PolicyConstants.AdminRole).ReturnsForAnyArgs(true);

        var userWithList = await readingListService.UserHasReadingListAccess(1, "majora2007");
        Assert.NotNull(userWithList);
        Assert.Single(userWithList.ReadingLists);
    }
    #endregion

    #region ValidateCBL

    [Fact]
    public async Task ValidateCblFile_ShouldFail_UserHasAccessToNoSeries()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = new SeriesBuilder("Fables").Build();
        var fables2Series = new SeriesBuilder("Fables: The Last Castle").Build();

        fablesSeries.Volumes.Add(new VolumeBuilder("1")
            .WithMinNumber(1)
            .WithName("2002")
            .WithChapter(new ChapterBuilder("1").Build())
            .WithChapter(new ChapterBuilder("2").Build())
            .WithChapter(new ChapterBuilder("3").Build())
            .Build()
        );
        fables2Series.Volumes.Add(new VolumeBuilder("1")
            .WithMinNumber(1)
            .WithName("2003")
            .WithChapter(new ChapterBuilder("1").Build())
            .WithChapter(new ChapterBuilder("2").Build())
            .WithChapter(new ChapterBuilder("3").Build())
            .Build()
        );

        context.AppUser.Add(new AppUserBuilder("majora2007", string.Empty).Build());

        context.Library.Add(new LibraryBuilder("Test LIb 2", LibraryType.Book)
            .WithSeries(fablesSeries)
            .WithSeries(fables2Series)
            .Build()
        );

        await unitOfWork.CommitAsync();

        var importSummary = await readingListService.ValidateCblFile(1, cblReadingList);

        Assert.Equal(CblImportResult.Fail, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);
    }

    [Fact]
    public async Task ValidateCblFile_ShouldFail_ServerHasNoSeries()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = new SeriesBuilder("Fablesa").Build();
        var fables2Series = new SeriesBuilder("Fablesa: The Last Castle").Build();

        fablesSeries.Volumes.Add(new VolumeBuilder("2002")
            .WithMinNumber(1)
            .WithChapter(new ChapterBuilder("1").Build())
            .WithChapter(new ChapterBuilder("2").Build())
            .WithChapter(new ChapterBuilder("3").Build())
            .Build());
        fables2Series.Volumes.Add(new VolumeBuilder("2003")
            .WithMinNumber(1)
            .WithChapter(new ChapterBuilder("1").Build())
            .WithChapter(new ChapterBuilder("2").Build())
            .WithChapter(new ChapterBuilder("3").Build())
            .Build());

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>(),
        });

        context.Library.Add(new LibraryBuilder("Test LIb 2", LibraryType.Book)
            .WithSeries(fablesSeries)
            .WithSeries(fables2Series)
            .Build());

        await unitOfWork.CommitAsync();

        var importSummary = await readingListService.ValidateCblFile(1, cblReadingList);

        Assert.Equal(CblImportResult.Fail, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);
    }

    #endregion

    #region CreateReadingListFromCBL

    private static CblReadingList LoadCblFromPath(string path)
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ReadingListService/");

        var reader = new System.Xml.Serialization.XmlSerializer(typeof(CblReadingList));
        using var file = new StreamReader(Path.Join(testDirectory, path));
        var cblReadingList = (CblReadingList) reader.Deserialize(file);
        file.Close();
        return cblReadingList;
    }

    [Fact]
    public async Task CreateReadingListFromCBL_ShouldCreateList()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = new SeriesBuilder("Fables")
            .WithVolume(new VolumeBuilder("2002")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .WithChapter(new ChapterBuilder("3").Build())
                .Build())
            .Build();

        var fables2Series = new SeriesBuilder("Fables: The Last Castle")
            .WithVolume(new VolumeBuilder("2003")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .WithChapter(new ChapterBuilder("3").Build())
                .Build())
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb 2", LibraryType.Book)
                    .WithSeries(fablesSeries)
                    .WithSeries(fables2Series)
                    .Build()
            },
        });
        await unitOfWork.CommitAsync();

        var importSummary = await readingListService.CreateReadingListFromCbl(1, cblReadingList);

        Assert.Equal(CblImportResult.Partial, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);

        var createdList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(1);

        Assert.NotNull(createdList);
        Assert.Equal("Fables", createdList.Title);

        Assert.Equal(4, createdList.Items.Count);
        Assert.Equal(1, createdList.Items.First(item => item.Order == 0).ChapterId);
        Assert.Equal(2, createdList.Items.First(item => item.Order == 1).ChapterId);
        Assert.Equal(3, createdList.Items.First(item => item.Order == 2).ChapterId);
        Assert.Equal(4, createdList.Items.First(item => item.Order == 3).ChapterId);
    }

    [Fact]
    public async Task CreateReadingListFromCBL_ShouldCreateList_ButOnlyIncludeSeriesThatUserHasAccessTo()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = new SeriesBuilder("Fables").Build();
        var fables2Series = new SeriesBuilder("Fables: The Last Castle").Build();

        fablesSeries.Volumes.Add(new VolumeBuilder("2002")
            .WithMinNumber(1)
            .WithChapter(new ChapterBuilder("1").Build())
            .WithChapter(new ChapterBuilder("2").Build())
            .WithChapter(new ChapterBuilder("3").Build())
            .Build());
        fables2Series.Volumes.Add(new VolumeBuilder("2003")
            .WithMinNumber(1)
            .WithChapter(new ChapterBuilder("1").Build())
            .WithChapter(new ChapterBuilder("2").Build())
            .WithChapter(new ChapterBuilder("3").Build())
            .Build());

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb 2", LibraryType.Book)
                    .WithSeries(fablesSeries)
                    .Build()
            },
        });

        context.Library.Add(new LibraryBuilder("Test LIb 2", LibraryType.Book)
            .WithSeries(fables2Series)
            .Build());

        await unitOfWork.CommitAsync();

        var importSummary = await readingListService.CreateReadingListFromCbl(1, cblReadingList);

        Assert.Equal(CblImportResult.Partial, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);

        var createdList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(1);

        Assert.NotNull(createdList);
        Assert.Equal("Fables", createdList.Title);

        Assert.Equal(3, createdList.Items.Count);
        Assert.Equal(1, createdList.Items.First(item => item.Order == 0).ChapterId);
        Assert.Equal(2, createdList.Items.First(item => item.Order == 1).ChapterId);
        Assert.Equal(3, createdList.Items.First(item => item.Order == 2).ChapterId);
        Assert.NotNull(importSummary.Results.SingleOrDefault(r => r.Series == "Fables: The Last Castle"
                                                                  && r.Reason == CblImportReason.SeriesMissing));
    }

    [Fact]
    public async Task CreateReadingListFromCBL_ShouldUpdateAnExistingList()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = new SeriesBuilder("Fables").Build();
        var fables2Series = new SeriesBuilder("Fables: The Last Castle").Build();

        fablesSeries.Volumes.Add(new VolumeBuilder("2002")
            .WithMinNumber(1)
            .WithChapter(new ChapterBuilder("1").Build())
            .WithChapter(new ChapterBuilder("2").Build())
            .WithChapter(new ChapterBuilder("3").Build())
            .Build());
        fables2Series.Volumes.Add(new VolumeBuilder("2003")
            .WithMinNumber(1)
            .WithChapter(new ChapterBuilder("1").Build())
            .WithChapter(new ChapterBuilder("2").Build())
            .WithChapter(new ChapterBuilder("3").Build())
            .Build());

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb 2", LibraryType.Book)
                    .WithSeries(fablesSeries)
                    .WithSeries(fables2Series)
                    .Build()
            },
        });

        await unitOfWork.CommitAsync();

        // Create a reading list named Fables and add 2 chapters to it
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        Assert.NotNull(user);
        var readingList = await readingListService.CreateReadingListForUser(user, "Fables");
        Assert.True(await readingListService.AddChaptersToReadingList(1, new List<int>() {1, 3}, readingList));
        Assert.Equal(2, readingList.Items.Count);

        // Attempt to import a Cbl with same reading list name
        var importSummary = await readingListService.CreateReadingListFromCbl(1, cblReadingList);

        Assert.Equal(CblImportResult.Partial, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);

        var createdList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(1);

        Assert.NotNull(createdList);
        Assert.Equal("Fables", createdList.Title);

        Assert.Equal(4, createdList.Items.Count);
        Assert.Equal(4, importSummary.SuccessfulInserts.Count);

        Assert.Equal(1, createdList.Items.First(item => item.Order == 0).ChapterId);
        Assert.Equal(3, createdList.Items.First(item => item.Order == 1).ChapterId); // we inserted 3 first
        Assert.Equal(2, createdList.Items.First(item => item.Order == 2).ChapterId);
        Assert.Equal(4, createdList.Items.First(item => item.Order == 3).ChapterId);
    }

    /// <summary>
    /// This test is about ensuring Annuals that are a separate series can be linked up properly (ComicVine)
    /// </summary>
    //[Fact]
    public async Task CreateReadingListFromCBL_ShouldCreateList_WithAnnuals()
    {
        // TODO: Implement this correctly
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readingListService, _) = Setup(unitOfWork, context, mapper);
        var cblReadingList = LoadCblFromPath("Annual.cbl");

        // Mock up our series
        var fablesSeries = new SeriesBuilder("Fables")
            .WithVolume(new VolumeBuilder("2002")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .WithChapter(new ChapterBuilder("3").Build())
                .Build())
            .Build();

        var fables2Series = new SeriesBuilder("Fables Annual")
            .WithVolume(new VolumeBuilder("2003")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new LibraryBuilder("Test LIb 2", LibraryType.Book)
                    .WithSeries(fablesSeries)
                    .WithSeries(fables2Series)
                    .Build()
            },
        });
        await unitOfWork.CommitAsync();

        var importSummary = await readingListService.CreateReadingListFromCbl(1, cblReadingList);

        Assert.Equal(CblImportResult.Success, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);

        var createdList = await unitOfWork.ReadingListRepository.GetReadingListByIdAsync(1);

        Assert.NotNull(createdList);
        Assert.Equal("Annual", createdList.Title);

        Assert.Equal(4, createdList.Items.Count);
        Assert.Equal(1, createdList.Items.First(item => item.Order == 0).ChapterId);
        Assert.Equal(2, createdList.Items.First(item => item.Order == 1).ChapterId);
        Assert.Equal(4, createdList.Items.First(item => item.Order == 2).ChapterId);
        Assert.Equal(3, createdList.Items.First(item => item.Order == 3).ChapterId);
    }

    #endregion

    #region CreateReadingListsFromSeries

    private async Task<Tuple<Series, Series>> SetupData(IUnitOfWork unitOfWork)
    {

        // Setup 2 series, only do this once tho
        if (await unitOfWork.SeriesRepository.DoesSeriesNameExistInLibrary("Series 1", 1, MangaFormat.Archive))
        {
            return new Tuple<Series, Series>(await unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(1),
                await unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(2));
        }

        var library =
            await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
                LibraryIncludes.Series | LibraryIncludes.AppUser);
        var user = new AppUserBuilder("majora2007", "majora2007@fake.com").Build();
        library!.AppUsers.Add(user);
        library.ManageReadingLists = true;

        // Setup the series for CreateReadingListsFromSeries
        var series1 = new SeriesBuilder("Series 1")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithStoryArc("CreateReadingListsFromSeries")
                    .WithStoryArcNumber("1")
                    .Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();

        var series2 = new SeriesBuilder("Series 2")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();

        library!.Series.Add(series1);
        library!.Series.Add(series2);

        await unitOfWork.CommitAsync();

        return new Tuple<Series, Series>(series1, series2);
    }

    // [Fact]
    // public async Task CreateReadingListsFromSeries_ShouldCreateFromSinglePair()
    // {
    //     //await SetupData();
    //
    //     var series1 = new SeriesBuilder("Series 1")
    //         .WithFormat(MangaFormat.Archive)
    //         .WithVolume(new VolumeBuilder("1")
    //             .WithChapter(new ChapterBuilder("1")
    //                 .WithStoryArc("CreateReadingListsFromSeries")
    //                 .WithStoryArcNumber("1")
    //                 .Build())
    //             .WithChapter(new ChapterBuilder("2").Build())
    //             .Build())
    //         .Build();
    //
    //     readingListService.CreateReadingListsFromSeries(series.Item1)
    // }

    #endregion
}
