using System.IO.Abstractions;
using System.Net.Http;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.API.Store;
using Kavita.Database.Extensions;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Kobo;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.Kobo;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Kobo;

public class KoboSyncServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private IKoboSyncService SetupSyncService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IReaderService? readerService = null,
        IBookService? bookService = null,
        IFileSystem? fileSystem = null,
        IKepubifyConverterService? kepubifyConverterService = null
        )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.AuthKey().Returns("abc");


        return new KoboSyncService(
            Substitute.For<ILogger<KoboSyncService>>(),
            unitOfWork,
            mapper,
            readerService ?? Substitute.For<IReaderService>(),
            bookService ?? Substitute.For<IBookService>(),
            Substitute.For<IReadingSessionService>(),
            userContext,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IHttpClientFactory>(),
            kepubifyConverterService ?? Substitute.For<IKepubifyConverterService>(),
            fileSystem ?? Substitute.For<IFileSystem>(),
            Substitute.For<ILocalizationService>()
            );
    }

    private async Task SetupCollection(IDataContext ctx)
    {
        var library = await ctx.Library.FirstAsync();

        var user = new AppUserBuilder("Amelia", "amelia@localhost")
            .WithLibrary(library)
            .WithRole(PolicyConstants.AdminRole)
            .Build();

        var series = new SeriesBuilder("Spice and Wolf")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(ParserConstants.LooseLeafVolume)
                    .WithTitle("Spice and Wolf")
                    .Build())
                .Build())
            .Build();

        var collection = new AppUserCollectionBuilder("Kobo Sync")
            .Build();
        user.Collections = [collection];

        ctx.Users.Add(user);
        ctx.Series.Add(series);

        await ctx.SaveChangesAsync();

        collection.Items = [series];

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task TestGetChangesSinceAsync_SyncCollection()
    {
        var (unitOfWork, ctx, mapper) = await CreateDatabase();
        var koboSyncService = SetupSyncService(unitOfWork, mapper);

        await SetupCollection(ctx);

        var syncPreferences = new AppUserKoboSyncPreferences
        {
            SyncCollection = 1,
        };

        var syncPoint = new KoboSyncPoint
        {
            UserId = 1,
            ChapterIds = [],
            ChapterIndex = 0,
            ChapterSyncTime = DateTime.MinValue,
            ProgressSyncTime = DateTime.MinValue,
            ProgressSyncedUntilId = 0,
            ProgressSyncedUntilIndex = 0
        };

        var (changes, exhausted) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);

        Assert.Single(changes);
        Assert.True(exhausted);

        var change = changes[0];
        Assert.NotNull(change.NewEntitlement);
        Assert.Equal("1", change.NewEntitlement.BookEntitlement.Id);

        (changes, _) =  await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        Assert.Empty(changes);

        var collection = await ctx.AppUserCollection.FirstAsync();
        collection.Items = [];
        await ctx.SaveChangesAsync();

        (changes, exhausted)  = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);

        Assert.Single(changes);
        Assert.True(exhausted);

        var change2 = changes[0];
        Assert.NotNull(change2.ChangedEntitlement);
        Assert.Equal("1", change2.ChangedEntitlement.BookEntitlement.Id);
        Assert.True(change2.ChangedEntitlement.BookEntitlement.IsRemoved);

        var series2 = new SeriesBuilder("The Moon on a Rainy Night")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(ParserConstants.LooseLeafVolume)
                    .Build())
                .Build())
            .Build();
        ctx.Series.Add(series2);
        collection.Items = [series2];
        await ctx.SaveChangesAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));

        (changes, exhausted) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);

        Assert.Single(changes);
        Assert.True(exhausted);
        var change3 = changes[0];
        Assert.NotNull(change3.NewEntitlement);
        Assert.Equal("2", change3.NewEntitlement.BookEntitlement.Id);
    }

    [Fact]
    public async Task UpdateProgress_ConversionFails_UsesFallbackProgress()
    {
        var kepubify = Substitute.For<IKepubifyConverterService>();
        kepubify.ConvertEpubToKepub(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var (unitOfWork, ctx, mapper) = await CreateDatabase();
        await SetupCollection(ctx);

        var chapter = await ctx.Chapter.Include(c => c.Files).FirstAsync();
        chapter.Files.Add(new MangaFileBuilder("/books/a.epub", MangaFormat.Epub).Build());
        await ctx.SaveChangesAsync();

        var reader = Substitute.For<IReaderService>();
        reader.SaveReadingProgress(Arg.Any<ProgressDto>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        var book = Substitute.For<IBookService>();
        book.GetPageForFile("/books/a.epub", "OEBPS/chapter.xhtml").Returns(5);

        kepubify.KepubifySupported.Returns(true);
        kepubify.BuildKepubifyPath(Arg.Any<int>()).Returns("/temp/a.kepub.epub");

        var state = new KoboReadingStateDto
        {
            EntitlementId = chapter.Id.ToString(),
            StatusInfo = new KoboStatusInfoDto { Status = KoboReadStatus.Reading },
            CurrentBookmark = new KoboBookmarkDto
            {
                Location = new KoboLocationDto { Source = "OEBPS/chapter.xhtml", Value = "kobo.1.1" },
            },
        };

        var service = SetupSyncService(unitOfWork, mapper, reader, book, kepubifyConverterService: kepubify);

        var result = await service.SaveProgress(1, chapter.Id, state, CancellationToken.None);

        Assert.Equal(KoboRequestResult.Success, result.RequestResult);
        await book.DidNotReceive().GetDocumentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>());
        await reader.Received(1).SaveReadingProgress(Arg.Is<ProgressDto>(p => p.PageNum == 5 && p.BookScrollId == null && p.KoboSpan == "kobo.1.1"),
            1, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AccumulateProgressChanges_DoesntLoop()
    {
        var (unitOfWork, ctx, mapper) = await CreateDatabase();
        var koboSyncService = SetupSyncService(unitOfWork, mapper);

        await SetupCollection(ctx);

        var syncPreferences = new AppUserKoboSyncPreferences {SyncItemLimit = 1};

        var series1 = await ctx.Series.Includes(SeriesIncludes.Chapters).FirstAsync();
        var series2 = new SeriesBuilder("The Moon on a Rainy Night")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(ParserConstants.LooseLeafVolume)
                    .Build())
                .Build())
            .Build();
        ctx.Series.Add(series2);
        await ctx.SaveChangesAsync();

        ctx.AppUserProgresses.Add(new AppUserProgress
        {
            PagesRead = 2,
            VolumeId = series1.Volumes.First().Id,
            SeriesId = series1.Id,
            LibraryId = 1,
            ChapterId = series1.Volumes.First().Chapters.First().Id,
            AppUserId = 1
        });
        ctx.AppUserProgresses.Add(new AppUserProgress
        {
            PagesRead = 2,
            VolumeId = series2.Volumes.First().Id,
            SeriesId = series2.Id,
            LibraryId = 1,
            ChapterId = series2.Volumes.First().Chapters.First().Id,
            AppUserId = 1
        });

        await ctx.SaveChangesAsync();

        var syncPoint = new KoboSyncPoint();

        var (changes, _) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        var readingChanges = changes.Where(c => c.ChangedReadingState != null).ToList();

        Assert.Single(readingChanges);
        Assert.Equal("1", readingChanges.First().ChangedReadingState.ReadingState.EntitlementId);

        (changes, _) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        readingChanges = changes.Where(c => c.ChangedReadingState != null).ToList();

        Assert.Single(readingChanges);
        Assert.Equal("2", readingChanges.First().ChangedReadingState.ReadingState.EntitlementId);

        (changes, _) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        readingChanges = changes.Where(c => c.ChangedReadingState != null).ToList();

        Assert.Empty(readingChanges);
    }

    [Fact]
    public async Task AccumulateChapterChanges_LargeChangeSet()
    {
        var (unitOfWork, ctx, mapper) = await CreateDatabase();
        var koboSyncService = SetupSyncService(unitOfWork, mapper);

        await SetupCollection(ctx);

        var series2 = new SeriesBuilder("The Moon on a Rainy Night")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(ParserConstants.LooseLeafVolume)
                    .Build())
                .Build())
            .Build();
        ctx.Series.Add(series2);

        var series3 = new SeriesBuilder("The Executioner and Her Way of Life")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(ParserConstants.LooseLeafVolume)
                    .Build())
                .Build())
            .Build();
        var series4 = new SeriesBuilder("There, Beneath the Water")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(ParserConstants.LooseLeafVolume)
                    .Build())
                .Build())
            .Build();

        ctx.Series.Add(series3);
        ctx.Series.Add(series4);

        await ctx.SaveChangesAsync();

        var col = await ctx.AppUserCollection.Includes(CollectionIncludes.Series).FirstAsync();
        col.Items.Add(series2);

        await ctx.SaveChangesAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));

        var syncPreferences = new AppUserKoboSyncPreferences {SyncItemLimit = 1, SyncCollection = 1};

        var syncPoint = new KoboSyncPoint
        {
            UserId = 1,
            ChapterIds = [],
            ChapterIndex = 0,
            ChapterSyncTime = DateTime.MinValue,
            ProgressSyncTime = DateTime.MinValue,
            ProgressSyncedUntilId = 0,
            ProgressSyncedUntilIndex = 0
        };

        var (changes, exhausted) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        Assert.Single(changes, c => c.NewEntitlement != null);
        Assert.Equal("1", changes.First().NewEntitlement.BookEntitlement.Id);
        Assert.False(exhausted);

        (changes, _) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        Assert.Single(changes, c => c.NewEntitlement != null);
        Assert.Equal("2", changes.First().NewEntitlement.BookEntitlement.Id);

        (changes, _) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        Assert.DoesNotContain(changes, c => c.NewEntitlement != null);

        await Task.Delay(TimeSpan.FromSeconds(1));

        col.Items.Add(series3);
        col.Items.Add(series4);
        await ctx.SaveChangesAsync();

        (changes,  _) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        Assert.Single(changes, c => c.NewEntitlement != null);
        Assert.Equal("3", changes.First().NewEntitlement.BookEntitlement.Id);

        (changes, exhausted) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        Assert.Single(changes, c => c.NewEntitlement != null);
        Assert.Equal("4", changes.First().NewEntitlement.BookEntitlement.Id);
        Assert.True(exhausted);
    }
}
