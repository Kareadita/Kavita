using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Reading;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.Plus;
using Kavita.Services.Plus.ScrobbleService;
using Kavita.Services.Reading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;
#nullable enable

public record ScrobbleProviderServices
{
    public required IScrobbleProviderService AniList { get; init; }
    public required IScrobbleProviderService Mal { get; init; }
    public required IScrobbleProviderService MangaBaka { get; init; }
    public required IScrobbleProviderService Hardcover { get; init; }
}

public class ScrobblingServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private const int ChapterPages = 100;

    /// <summary>
    /// {
    /// "Issuer": "Issuer",
    /// "Issued At": "2025-06-15T21:01:57.615Z",
    /// "Expiration": "2200-06-15T21:01:57.615Z"
    /// }
    /// </summary>
    /// <remarks>Our UnitTests will fail in 2200 :(</remarks>
    private const string ValidJwtToken =
        "eyJhbGciOiJIUzI1NiJ9.eyJJc3N1ZXIiOiJJc3N1ZXIiLCJleHAiOjcyNzI0NTAxMTcsImlhdCI6MTc1MDAyMTMxN30.zADmcGq_BfxbcV8vy4xw5Cbzn4COkmVINxgqpuL17Ng";

    /// <summary>
    ///
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="context"></param>
    /// <returns>First IReaderService is not hooked up to the scrobbleService, second one is</returns>
    public async Task<(ScrobblingService, ILicenseService, IKavitaPlusApiService, IReaderService, IReaderService, ScrobbleProviderServices)> Setup(IUnitOfWork unitOfWork, DataContext context)
    {
        var licenseService = Substitute.For<ILicenseService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var logger = Substitute.For<ILogger<ScrobblingService>>();
        var emailService = Substitute.For<IEmailService>();
        var kavitaPlusApiService = Substitute.For<IKavitaPlusApiService>();

        var aniList = new AniListScrobbleProviderService(Substitute.For<ILogger<AniListScrobbleProviderService>>(), unitOfWork, Substitute.For<IKavitaPlusAuditService>());
        var mal = new MyAnimeListScrobbleProviderService(Substitute.For<ILogger<MyAnimeListScrobbleProviderService>>(), unitOfWork, Substitute.For<IKavitaPlusAuditService>());
        var mangaBaka = new MangabakaScrobbleProviderService(Substitute.For<ILogger<MangabakaScrobbleProviderService>>(), unitOfWork, Substitute.For<IKavitaPlusAuditService>());
        var hardcover = new HardcoverScrobbleProviderService(Substitute.For<ILogger<HardcoverScrobbleProviderService>>(), unitOfWork, Substitute.For<IKavitaPlusAuditService>());

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddKeyedScoped<IScrobbleProviderService>(ScrobbleProvider.AniList, (_, _) => aniList);
        serviceCollection.AddKeyedScoped<IScrobbleProviderService>(ScrobbleProvider.Mal, (_, _) => mal);
        serviceCollection.AddKeyedScoped<IScrobbleProviderService>(ScrobbleProvider.MangaBaka, (_, _) => mangaBaka);
        serviceCollection.AddKeyedScoped<IScrobbleProviderService>(ScrobbleProvider.Hardcover, (_, _) => hardcover);

        var ruleService = new ScrobbleRuleService(unitOfWork, Substitute.For<ILogger<ScrobbleRuleService>>());

        var service = new ScrobblingService(unitOfWork, Substitute.For<IEventHub>(), logger,  licenseService,
            localizationService, emailService, kavitaPlusApiService, serviceCollection.BuildServiceProvider(),
            Substitute.For<IKavitaPlusAuditService>(), ruleService);

        var readerService = new ReaderService(unitOfWork,
            Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(),
            Substitute.For<IScrobblingService>(), Substitute.For<IReadingSessionService>(),
            Substitute.For<IClientInfoAccessor>(), Substitute.For<ISeriesService>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>(), Substitute.For<IBookService>()); // Do not use the actual one

        var hookedUpReaderService = new ReaderService(unitOfWork,
            Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(),
            service, Substitute.For<IReadingSessionService>(),
            Substitute.For<IClientInfoAccessor>(), Substitute.For<ISeriesService>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>(), Substitute.For<IBookService>());

        await SeedData(unitOfWork, context);

        return (service, licenseService, kavitaPlusApiService, readerService, hookedUpReaderService, new ScrobbleProviderServices
        {
            AniList = aniList,
            Hardcover = hardcover,
            Mal = mal,
            MangaBaka = mangaBaka,
        });
    }

    private async Task SeedData(IUnitOfWork unitOfWork, DataContext context)
    {
        var series = new SeriesBuilder("Test Series")
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .WithVolume(new VolumeBuilder("Volume 1")
                .WithChapters([
                    new ChapterBuilder("1")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("2")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("3")
                        .WithPages(ChapterPages)
                        .Build()])
                .Build())
            .WithVolume(new VolumeBuilder("Volume 2")
                .WithChapters([
                    new ChapterBuilder("4")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("5")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("6")
                        .WithPages(ChapterPages)
                        .Build()])
                .Build())
            .Build();

        var library = new LibraryBuilder("Test Library", LibraryType.Manga)
            .WithAllowScrobbling(true)
            .WithSeries(series)
            .Build();


        context.Library.Add(library);

        var user = new AppUserBuilder("testuser", "testuser")
            .Build();

        AccountService.AddScrobbleProvidersToUser(user);

        user.ScrobbleProviders[ScrobbleProvider.AniList] = new AppUserScrobbleProvider
        {
            AuthenticationToken = ValidJwtToken,
            Settings = new ScrobbleProviderSettingsDto
            {
                ProgressScrobbling = true,
                RatingScrobbling = true,
                ReviewsScrobbling = true,
                AllLibraries = true,
                WantToReadSync = true,
            }
        };

        unitOfWork.UserRepository.Add(user);

        await unitOfWork.CommitAsync();
    }

    private async Task<ScrobbleEvent> CreateScrobbleEvent(IUnitOfWork unitOfWork, int? seriesId = null)
    {
        // var (unitOfWork, context, _) = await CreateDatabase();
        // await Setup(unitOfWork, context);
        //

        var evt = new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.ChapterRead,
            Format = PlusMediaFormat.Manga,
            SeriesId = seriesId ?? 0,
            LibraryId = 0,
            AppUserId = 0,
        };

        if (seriesId != null)
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId.Value);
            if (series != null) evt.Series = series;
        }

        return evt;
    }


    #region K+ API Request Tests

    [Fact]
    public async Task PostScrobbleUpdate_AuthErrors()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, _, kavitaPlusApiService, _, _, _) = await Setup(unitOfWork, context);

        kavitaPlusApiService.PostScrobbleV3UpdateAsync(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Unauthorized"
            });

        var evt = await CreateScrobbleEvent(unitOfWork);
        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await service.PostScrobbleUpdate(new ScrobbleV3Dto
            {
                AuthenticationToken = null,
                Provider = ScrobbleProvider.AniList,
                SeriesName = null,
                Format = PlusMediaFormat.Manga,
            }, string.Empty, evt);
        });
        Assert.True(evt.IsErrored);
        Assert.Equal("Kavita+ subscription no longer active", evt.ErrorDetails);
    }

    [Fact]
    public async Task PostScrobbleUpdate_UnknownSeriesLoggedAsError()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, _, kavitaPlusApiService, _, _, _) = await Setup(unitOfWork, context);

        kavitaPlusApiService.PostScrobbleV3UpdateAsync(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Unknown Series"
            });

        var evt = await CreateScrobbleEvent(unitOfWork, 1);

        await Assert.ThrowsAsync<KavitaException>(() => service.PostScrobbleUpdate(new ScrobbleV3Dto
        {
            AuthenticationToken = null,
            Provider = ScrobbleProvider.AniList,
            SeriesName = null,
            Format = PlusMediaFormat.Manga,
        }, string.Empty, evt));
        await unitOfWork.CommitAsync();
        Assert.True(evt.IsErrored);

        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series);
        Assert.True(series.IsBlacklisted);

        var errors = await unitOfWork.ScrobbleRepository.GetAllScrobbleErrorsForSeries(1);
        Assert.Single(errors);
        Assert.Equal("Series cannot be matched for Scrobbling", errors.First().Comment);
        Assert.Equal(series.Id, errors.First().SeriesId);
    }

    [Fact]
    public async Task PostScrobbleUpdate_InvalidAccessToken()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, _, kavitaPlusApiService, _, _, _) = await Setup(unitOfWork, context);

        kavitaPlusApiService.PostScrobbleV3UpdateAsync(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Access token is invalid"
            });

        var evt = await CreateScrobbleEvent(unitOfWork);

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await service.PostScrobbleUpdate(new ScrobbleV3Dto
            {
                AuthenticationToken = null,
                Provider = ScrobbleProvider.AniList,
                SeriesName = null,
                Format = PlusMediaFormat.Manga,
            }, string.Empty, evt);
        });

        Assert.True(evt.IsErrored);
        Assert.Equal("Access Token needs to be rotated to continue scrobbling", evt.ErrorDetails);
    }

    #endregion

    #region K+ API Request data tests

    [Fact]
    public async Task ProcessReadEvents_CreatesNoEventsWhenNoProgress()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, kavitaPlusApiService, _, _, _) = await Setup(unitOfWork, context);

        // Set Returns
        licenseService.HasActiveLicense().Returns(Task.FromResult(true));
        kavitaPlusApiService.GetRateLimitAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(100);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(4);
        Assert.NotNull(chapter);

        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        // Call Scrobble without having any progress
        await service.ScrobbleReadingUpdate(1, 1, chapter.Id);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ProcessReadEvents_UpdateVolumeAndChapterData()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, kavitaPlusApiService, readerService, _, _) = await Setup(unitOfWork, context);

        // Set Returns
        licenseService.HasActiveLicense().Returns(Task.FromResult(true));
        kavitaPlusApiService.GetRateLimitForProviderAsync(ScrobbleProvider.AniList, Arg.Any<string>(), Arg.Any<string>())
            .Returns(KPlusResult<int>.Success(100));

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(4);
        Assert.NotNull(chapter);

        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        // Mark something as read to trigger event creation
        await readerService.MarkChaptersAsRead(user, 1, new List<Chapter>() {volume.Chapters[0]});
        await unitOfWork.CommitAsync();

        // Call Scrobble while having some progress
        await service.ScrobbleReadingUpdate(user.Id, 1, chapter.Id);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);

        // Give it some (more) read progress
        await readerService.MarkChaptersAsRead(user, 1, volume.Chapters);
        await readerService.MarkChaptersAsRead(user, 1, [chapter]);
        await unitOfWork.CommitAsync();

        await service.ProcessUpdatesSinceLastSync();

        await kavitaPlusApiService.Received(1).PostScrobbleV3UpdateAsync(
            Arg.Is<ScrobbleV3Dto>(data =>
                data.ChapterNumber == (int)chapter.MaxNumber &&
                data.VolumeNumber == (int)volume.MaxNumber
            ),
            Arg.Any<string>());
    }

    #endregion

    #region Scrobble Reading Update Tests

    [Fact]
    public async Task ScrobbleReadingUpdate_IgnoreNoLicense()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(false);

        await service.ScrobbleReadingUpdate(1, 1, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ScrobbleReadingUpdate_RemoveWhenNoProgress()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, readerService, hookedUpReaderService, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        await readerService.MarkChaptersAsRead(user, 1, new List<Chapter>() {volume.Chapters[0]});
        await unitOfWork.CommitAsync();

        await service.ScrobbleReadingUpdate(1, 1, volume.Chapters[0].Id);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);

        var readEvent = events.First();
        Assert.False(readEvent.IsProcessed);

        await hookedUpReaderService.MarkSeriesAsUnread(user, 1);
        await unitOfWork.CommitAsync();

        // Existing event is deleted
        await service.ScrobbleReadingUpdate(1, 1, volume.Chapters[0].Id);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        await hookedUpReaderService.MarkSeriesAsUnread(user, 1);
        await unitOfWork.CommitAsync();

        // No new events are added
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ScrobbleReadingUpdate_UpdateExistingNotIsProcessed()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, readerService, _, _) = await Setup(unitOfWork, context);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var chapter1 = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        var chapter2 = await unitOfWork.ChapterRepository.GetChapterAsync(2);
        var chapter3 = await unitOfWork.ChapterRepository.GetChapterAsync(3);
        Assert.NotNull(chapter1);
        Assert.NotNull(chapter2);
        Assert.NotNull(chapter3);

        licenseService.HasActiveLicense().Returns(true);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);


        await readerService.MarkChaptersAsRead(user, 1, [chapter1]);
        await unitOfWork.CommitAsync();

        // Scrobble update
        await service.ScrobbleReadingUpdate(1, 1, chapter1.Id);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);

        var readEvent = events[0];
        Assert.False(readEvent.IsProcessed);
        Assert.Equal(1, readEvent.ChapterNumber);

        // Mark as processed
        readEvent.IsProcessed = true;
        await unitOfWork.CommitAsync();

        await readerService.MarkChaptersAsRead(user, 1, [chapter2]);
        await unitOfWork.CommitAsync();

        // Scrobble update
        await service.ScrobbleReadingUpdate(1, 1, chapter1.Id);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Equal(2, events.Count);
        Assert.Single(events.Where(e => e.IsProcessed).ToList());
        Assert.Single(events.Where(e => !e.IsProcessed).ToList());

        // Should update the existing non processed event
        await readerService.MarkChaptersAsRead(user, 1, [chapter3]);
        await unitOfWork.CommitAsync();

        // Scrobble update
        await service.ScrobbleReadingUpdate(1, 1, chapter1.Id);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Equal(2, events.Count);
        Assert.Single(events.Where(e => e.IsProcessed).ToList());
        Assert.Single(events.Where(e => !e.IsProcessed).ToList());
    }

    #endregion

    #region ScrobbleWantToReadUpdate Tests

    [Fact]
    public async Task ScrobbleWantToReadUpdate_NoExistingEvents_WantToRead_ShouldCreateNewEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // Act
        await service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Assert
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.AddWantToRead, events[0].ScrobbleEventType);
        Assert.Equal(userId, events[0].AppUserId);
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_NoExistingEvents_RemoveWantToRead_ShouldCreateNewEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // Act
        await service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Assert
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.RemoveWantToRead, events[0].ScrobbleEventType);
        Assert.Equal(userId, events[0].AppUserId);
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_ExistingWantToReadEvent_WantToRead_ShouldNotCreateNewEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // First, let's create an event through the service
        await service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Act - Try to create the same event again
        await service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Assert
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);

        Assert.Single(events);
        Assert.All(events, e => Assert.Equal(ScrobbleEventType.AddWantToRead, e.ScrobbleEventType));
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_ExistingWantToReadEvent_RemoveWantToRead_ShouldAddRemoveEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // First, let's create a want-to-read event through the service
        await service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Act - Now remove from want-to-read
        await service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Assert
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);

        Assert.Single(events);
        Assert.Contains(events, e => e.ScrobbleEventType == ScrobbleEventType.RemoveWantToRead);
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_ExistingRemoveWantToReadEvent_RemoveWantToRead_ShouldNotCreateNewEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // First, let's create a remove-from-want-to-read event through the service
        await service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Act - Try to create the same event again
        await service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Assert
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);

        Assert.Single(events);
        Assert.All(events, e => Assert.Equal(ScrobbleEventType.RemoveWantToRead, e.ScrobbleEventType));
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_ExistingRemoveWantToReadEvent_WantToRead_ShouldAddWantToReadEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // First, let's create a remove-from-want-to-read event through the service
        await service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Act - Now add to want-to-read
        await service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Assert
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);

        Assert.Single(events);
        Assert.Contains(events, e => e.ScrobbleEventType == ScrobbleEventType.AddWantToRead);
    }

    #endregion

    #region Scrobble Rating Update Test

    [Fact]
    public async Task ScrobbleRatingUpdate_IgnoreNoLicense()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(false);

        await service.ScrobbleSeriesRatingUpdate(1, 1, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ScrobbleRatingUpdate_UpdateExistingNotIsProcessed()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series);

        await service.ScrobbleSeriesRatingUpdate(user.Id, series.Id, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);
        Assert.Equal(1, events.First().Rating);

        // Mark as processed
        events.First().IsProcessed = true;
        await unitOfWork.CommitAsync();

        await service.ScrobbleSeriesRatingUpdate(user.Id, series.Id, 5);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Equal(2, events.Count);
        Assert.Single(events, evt => evt.IsProcessed);
        Assert.Single(events, evt => !evt.IsProcessed);

        await service.ScrobbleSeriesRatingUpdate(user.Id, series.Id, 5);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events, evt => !evt.IsProcessed);
        Assert.Equal(5, events.First(evt => !evt.IsProcessed).Rating);

    }

    #endregion

    #region CreateEventsFromExistingHistory Tests

    private static async Task<int> GetTestLibraryIdAsync(DataContext context)
    {
        return await context.Library
            .Where(l => l.Name == "Test Library")
            .Select(l => l.Id)
            .FirstAsync();
    }

    private static async Task LinkUserToTestLibraryAsync(IUnitOfWork unitOfWork, DataContext context, int userId)
    {
        var libraryId = await GetTestLibraryIdAsync(context);
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.AppUser);
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId);
        Assert.NotNull(library);
        Assert.NotNull(user);
        if (library.AppUsers.Any(u => u.Id == userId)) return;
        library.AppUsers.Add(user);
        await unitOfWork.CommitAsync();
    }

    [Fact]
    public async Task CreateEventsFromExistingHistory_NoLicense_DoesNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, readerService, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(false);

        // Seed something that would produce an event if the method ran
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);
        var chapter1 = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(chapter1);
        await readerService.MarkChaptersAsRead(user, 1, [chapter1]);
        await unitOfWork.CommitAsync();

        await service.CreateEventsFromExistingHistory(ScrobbleProvider.AniList);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        var reloaded = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded.ScrobbleProviders.Values
            .Where(p => p.ScrobbleEventGenerationRan >= DateTime.UtcNow.AddDays(-1))
            .ToList());
    }

    [Fact]
    public async Task CreateEventsFromExistingHistory_SpecificUser_NoAniListToken_DoesNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        // Seed a rating that would otherwise create an event
        context.AppUserRating.Add(new AppUserRating
        {
            AppUserId = 1,
            SeriesId = 1,
            Rating = 4f,
            HasBeenRated = true,
        });

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);
        user.ScrobbleProviders[ScrobbleProvider.AniList].AuthenticationToken = "";

        await unitOfWork.CommitAsync();

        // User has no AniListAccessToken, guard at line 1038 should short-circuit
        await service.CreateEventsFromExistingHistory(ScrobbleProvider.AniList, userId: 1);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        var reloaded = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.ScrobbleProviders[ScrobbleProvider.AniList].ScrobbleEventGenerationRan > DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task CreateEventsFromExistingHistory_WantToRead_CreatesAddWantToReadEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        await LinkUserToTestLibraryAsync(unitOfWork, context, 1);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.WantToRead);
        Assert.NotNull(user);
        user.WantToRead.Add(new AppUserWantToRead { SeriesId = 1 });
        await unitOfWork.CommitAsync();

        var before = DateTime.UtcNow.AddSeconds(-1);

        await service.CreateEventsFromExistingHistory(ScrobbleProvider.AniList, userId: 1);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.AddWantToRead, events[0].ScrobbleEventType);
        Assert.Equal(1, events[0].AppUserId);

        var reloaded = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.ScrobbleProviders[ScrobbleProvider.AniList].ScrobbleEventGenerationRan >= before);
    }

    [Fact]
    public async Task CreateEventsFromExistingHistory_Rating_CreatesScoreUpdatedEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        context.AppUserRating.Add(new AppUserRating
        {
            AppUserId = 1,
            SeriesId = 1,
            Rating = 4.5f,
            HasBeenRated = true,
        });
        await unitOfWork.CommitAsync();

        await service.CreateEventsFromExistingHistory(ScrobbleProvider.AniList, userId: 1);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.ScoreUpdated, events[0].ScrobbleEventType);
        Assert.Equal(4.5f, events[0].Rating);
        Assert.Equal(1, events[0].AppUserId);
    }

    [Fact]
    public async Task CreateEventsFromExistingHistory_Reading_CreatesChapterReadEvents()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, readerService, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        await LinkUserToTestLibraryAsync(unitOfWork, context, 1);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var chapter1 = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(chapter1);
        await readerService.MarkChaptersAsRead(user, 1, [chapter1]);
        await unitOfWork.CommitAsync();


        var progressPagesRead = await context.AppUserProgresses
            .Where(p => p.AppUserId == 1 && p.SeriesId == 1)
            .SumAsync(p => p.PagesRead);
        Assert.Equal(ChapterPages, progressPagesRead);

        await service.CreateEventsFromExistingHistory(ScrobbleProvider.AniList, userId: 1);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.NotEmpty(events);
    }

    [Fact]
    public async Task CreateEventsFromExistingHistory_LibraryScrobblingDisabled_SkipsAllWorkButSetsFlag()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, readerService, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        await LinkUserToTestLibraryAsync(unitOfWork, context, 1);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.WantToRead);
        Assert.NotNull(user);
        user.WantToRead.Add(new AppUserWantToRead { SeriesId = 1 });
        await unitOfWork.CommitAsync();

        context.AppUserRating.Add(new AppUserRating
        {
            AppUserId = 1,
            SeriesId = 1,
            Rating = 4f,
            HasBeenRated = true,
        });
        await unitOfWork.CommitAsync();

        var chapter1 = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(chapter1);
        await readerService.MarkChaptersAsRead(user, 1, [chapter1]);
        await unitOfWork.CommitAsync();

        // Flip the test library to disallow scrobbling AFTER seeding the data
        var testLibraryId = await GetTestLibraryIdAsync(context);
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(testLibraryId);
        Assert.NotNull(library);

        user.ScrobbleProviders[ScrobbleProvider.AniList].Settings.AllLibraries = false;

        await unitOfWork.CommitAsync();

        await service.CreateEventsFromExistingHistory(ScrobbleProvider.AniList, userId: 1);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        var reloaded = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.ScrobbleProviders[ScrobbleProvider.AniList].ScrobbleEventGenerationRan > DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task CreateEventsFromExistingHistory_SetsHasRunFlagAndTimestamp()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);
        await unitOfWork.CommitAsync();

        var before = DateTime.UtcNow.AddSeconds(-1);

        await service.CreateEventsFromExistingHistory(ScrobbleProvider.AniList, userId: 1);

        var reloaded = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.ScrobbleProviders[ScrobbleProvider.AniList].ScrobbleEventGenerationRan >= before);
    }

    #endregion

    #region Scrobble settings test

    [Fact]
    public async Task ScrobbleSettings_ScrobbleEventToggles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        user.ScrobbleProviders[ScrobbleProvider.AniList].Settings = new ScrobbleProviderSettingsDto
        {
            AllLibraries = true,
            RatingScrobbling = false,
            ProgressScrobbling = false,
            ReviewsScrobbling = false,
            WantToReadSync = true,
        };

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        await service.ScrobbleReadingUpdate(1, 1, 1);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        await service.ScrobbleSeriesRatingUpdate(1, 1, 5);

        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        await service.ScrobbleChapterRatingUpdate(1, 1, 1, 5);

        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        await service.ScrobbleSeriesReviewUpdate(1, 1, string.Empty, "test");

        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        await service.ScrobbleChapterReviewUpdate(1, 1, 1, string.Empty, "test");

        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        await service.ScrobbleWantToReadUpdate(1, 1, false);

        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.RemoveWantToRead, events[0].ScrobbleEventType);
    }

    [Fact]
    public async Task ScrobbleSettings_Libraries()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        user.ScrobbleProviders[ScrobbleProvider.AniList].Settings.AllLibraries = false;
        user.ScrobbleProviders[ScrobbleProvider.AniList].Settings.Libraries = [1]; // Manga library from base class

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        var testLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(await GetTestLibraryIdAsync(context));
        Assert.NotNull(testLib);

        var lib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1, LibraryIncludes.Series);
        Assert.NotNull(lib);

        var series = new SeriesBuilder("Spice and Wolf")
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .WithVolume(new VolumeBuilder("Volume 1")
                .WithChapters([
                    new ChapterBuilder("1")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("2")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("3")
                        .WithPages(ChapterPages)
                        .Build()])
                .Build())
            .WithVolume(new VolumeBuilder("Volume 2")
                .WithChapters([
                    new ChapterBuilder("4")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("5")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("6")
                        .WithPages(ChapterPages)
                        .Build()])
                .Build())
            .Build();

        lib.Series.Add(series);

        await unitOfWork.CommitAsync();

        var testSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(testLib.Series.First().Id, SeriesIncludes.Chapters);
        Assert.NotNull(testSeries);

        await service.ScrobbleSeriesRatingUpdate(1, testSeries.Id, 5);
        await service.ScrobbleSeriesRatingUpdate(1, series.Id, 5);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(series.Id);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.ScoreUpdated, events[0].ScrobbleEventType);
        Assert.Equal(series.Id, events[0].SeriesId);
    }

    [Fact]
    public async Task ScrobbleSettings_HighestAgeRating()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        user.ScrobbleProviders[ScrobbleProvider.AniList].Settings.HighestAgeRating = AgeRating.Mature;

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        var lib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1, LibraryIncludes.Series);
        Assert.NotNull(lib);

        var series = new SeriesBuilder("Spice and Wolf")
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.R18Plus)
                .Build())
            .WithVolume(new VolumeBuilder("Volume 1")
                .WithChapters([
                    new ChapterBuilder("1")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("2")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("3")
                        .WithPages(ChapterPages)
                        .Build()])
                .Build())
            .WithVolume(new VolumeBuilder("Volume 2")
                .WithChapters([
                    new ChapterBuilder("4")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("5")
                        .WithPages(ChapterPages)
                        .Build(),
                    new ChapterBuilder("6")
                        .WithPages(ChapterPages)
                        .Build()])
                .Build())
            .Build();

        lib.Series.Add(series);

        await unitOfWork.CommitAsync();

        await service.ScrobbleSeriesRatingUpdate(1, series.Id, 5);

        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(series.Id);
        Assert.Empty(events);

        series.Metadata.AgeRating = AgeRating.Everyone;

        await unitOfWork.CommitAsync();

        await service.ScrobbleSeriesRatingUpdate(1, series.Id, 5);

        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(series.Id);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.ScoreUpdated, events[0].ScrobbleEventType);
        Assert.Equal(series.Id, events[0].SeriesId);
    }

    #endregion

    #region Read Status Transition Rule Dedup

    [Fact]
    public async Task RunReadStatusTransitionRules_DoesNotResendTransition_AfterDelivery()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, kavitaPlusApiService, readerService, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(Task.FromResult(true));
        kavitaPlusApiService.GetRateLimitForProviderAsync(ScrobbleProvider.AniList, Arg.Any<string>(), Arg.Any<string>())
            .Returns(KPlusResult<int>.Success(100));
        kavitaPlusApiService.PostScrobbleV3UpdateAsync(Arg.Any<ScrobbleV3Dto>(), Arg.Any<string>())
            .Returns(new ScrobbleResponseDto { Successful = true, RateLeft = 100 });

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        // Enable the inactive-series rule
        user.ScrobbleProviders[ScrobbleProvider.AniList].Settings.InactiveSeriesRule = new ReadStatusTransitionRule
        {
            Enabled = true,
            Days = 30,
            TransitionStatus = ScrobbleReadStatus.OnHold,
            ExcludedPublicationStatus = [],
        };
        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        // Give the series some progress, then age it past the rule window so it qualifies as "inactive"
        var chapter1 = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(chapter1);
        await readerService.MarkChaptersAsRead(user, 1, [chapter1]);
        await unitOfWork.CommitAsync();

        var aged = await context.AppUserProgresses
            .Where(p => p.AppUserId == 1 && p.SeriesId == 1)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastModifiedUtc, DateTime.UtcNow.AddDays(-60)));
        Assert.Equal(1, aged);

        // First run: the rule fires and creates a single read-status event stamped with the rule kind + hash
        await service.RunReadStatusTransitionRules();

        // Clearing the tracker forces the assertions below to read the persisted columns, not in-memory copies
        context.ChangeTracker.Clear();
        var afterFirstRun = (await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1))
            .Where(e => e.ScrobbleEventType == ScrobbleEventType.ReadStatusUpdate)
            .ToList();
        Assert.Single(afterFirstRun);
        Assert.Equal(TransitionRuleKind.Inactive, afterFirstRun[0].TransitionRuleKind);
        Assert.False(string.IsNullOrEmpty(afterFirstRun[0].RuleHashSnapshot));

        // Deliver it through the real pipeline -> writes the durable ledger row
        await service.ProcessUpdatesSinceLastSync();

        await kavitaPlusApiService.Received().PostScrobbleV3UpdateAsync(
            Arg.Is<ScrobbleV3Dto>(d => d.ScrobbleEventType == ScrobbleEventType.ReadStatusUpdate), Arg.Any<string>());
        Assert.Equal(1, await context.ScrobbleRuleHistory.CountAsync());

        // Second run: the series is still inactive, but the ledger must suppress a re-send
        await service.RunReadStatusTransitionRules();

        var afterSecondRun = (await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1))
            .Where(e => e.ScrobbleEventType == ScrobbleEventType.ReadStatusUpdate)
            .ToList();
        Assert.Single(afterSecondRun);
        Assert.Equal(afterFirstRun[0].Id, afterSecondRun[0].Id);
    }

    #endregion
}
