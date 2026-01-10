using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Scrobble;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Reading;
using API.SignalR;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;
#nullable enable

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
    public async Task<(ScrobblingService, ILicenseService, IKavitaPlusApiService, IReaderService, IReaderService)> Setup(IUnitOfWork unitOfWork, DataContext context)
    {
        var licenseService = Substitute.For<ILicenseService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var logger = Substitute.For<ILogger<ScrobblingService>>();
        var emailService = Substitute.For<IEmailService>();
        var kavitaPlusApiService = Substitute.For<IKavitaPlusApiService>();

        var service = new ScrobblingService(unitOfWork, Substitute.For<IEventHub>(), logger,  licenseService,
            localizationService, emailService, kavitaPlusApiService);

        var readerService = new ReaderService(unitOfWork,
            Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(),
            Substitute.For<IScrobblingService>(), Substitute.For<IReadingSessionService>(),
            Substitute.For<IClientInfoAccessor>(), Substitute.For<ISeriesService>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>()); // Do not use the actual one

        var hookedUpReaderService = new ReaderService(unitOfWork,
            Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(),
            service, Substitute.For<IReadingSessionService>(),
            Substitute.For<IClientInfoAccessor>(), Substitute.For<ISeriesService>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>());

        await SeedData(unitOfWork, context);

        return (service, licenseService, kavitaPlusApiService, readerService, hookedUpReaderService);
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
            //.WithPreferences(new UserPreferencesBuilder().WithAniListScrobblingEnabled(true).Build())
            .Build();

        user.UserPreferences.AniListScrobblingEnabled = true;

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
        var (service, _, kavitaPlusApiService, _, _) = await Setup(unitOfWork, context);

        kavitaPlusApiService.PostScrobbleUpdate(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Unauthorized"
            });

        var evt = await CreateScrobbleEvent(unitOfWork);
        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await service.PostScrobbleUpdate(new ScrobbleDto(), "", evt);
        });
        Assert.True(evt.IsErrored);
        Assert.Equal("Kavita+ subscription no longer active", evt.ErrorDetails);
    }

    [Fact]
    public async Task PostScrobbleUpdate_UnknownSeriesLoggedAsError()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, _, kavitaPlusApiService, _, _) = await Setup(unitOfWork, context);

        kavitaPlusApiService.PostScrobbleUpdate(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Unknown Series"
            });

        var evt = await CreateScrobbleEvent(unitOfWork, 1);

        await service.PostScrobbleUpdate(new ScrobbleDto(), string.Empty, evt);
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
        var (service, _, kavitaPlusApiService, _, _) = await Setup(unitOfWork, context);

        kavitaPlusApiService.PostScrobbleUpdate(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Access token is invalid"
            });

        var evt = await CreateScrobbleEvent(unitOfWork);

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await service.PostScrobbleUpdate(new ScrobbleDto(), "", evt);
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
        var (service, licenseService, kavitaPlusApiService, _, _) = await Setup(unitOfWork, context);

        // Set Returns
        licenseService.HasActiveLicense().Returns(Task.FromResult(true));
        kavitaPlusApiService.GetRateLimit(Arg.Any<string>(), Arg.Any<string>())
            .Returns(100);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        // Ensure CanProcessScrobbleEvent returns true
        user.AniListAccessToken = ValidJwtToken;
        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(4);
        Assert.NotNull(chapter);

        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        // Call Scrobble without having any progress
        await service.ScrobbleReadingUpdate(1, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ProcessReadEvents_UpdateVolumeAndChapterData()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, kavitaPlusApiService, readerService, _) = await Setup(unitOfWork, context);

        // Set Returns
        licenseService.HasActiveLicense().Returns(Task.FromResult(true));
        kavitaPlusApiService.GetRateLimit(Arg.Any<string>(), Arg.Any<string>())
            .Returns(100);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        // Ensure CanProcessScrobbleEvent returns true
        user.AniListAccessToken = ValidJwtToken;
        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync();

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(4);
        Assert.NotNull(chapter);

        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        // Mark something as read to trigger event creation
        await readerService.MarkChaptersAsRead(user, 1, new List<Chapter>() {volume.Chapters[0]});
        await unitOfWork.CommitAsync();

        // Call Scrobble while having some progress
        await service.ScrobbleReadingUpdate(user.Id, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);

        // Give it some (more) read progress
        await readerService.MarkChaptersAsRead(user, 1, volume.Chapters);
        await readerService.MarkChaptersAsRead(user, 1, [chapter]);
        await unitOfWork.CommitAsync();

        await service.ProcessUpdatesSinceLastSync();

        await kavitaPlusApiService.Received(1).PostScrobbleUpdate(
            Arg.Is<ScrobbleDto>(data =>
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
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(false);

        await service.ScrobbleReadingUpdate(1, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ScrobbleReadingUpdate_RemoveWhenNoProgress()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, readerService, hookedUpReaderService) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        await readerService.MarkChaptersAsRead(user, 1, new List<Chapter>() {volume.Chapters[0]});
        await unitOfWork.CommitAsync();

        await service.ScrobbleReadingUpdate(1, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);

        var readEvent = events.First();
        Assert.False(readEvent.IsProcessed);

        await hookedUpReaderService.MarkSeriesAsUnread(user, 1);
        await unitOfWork.CommitAsync();

        // Existing event is deleted
        await service.ScrobbleReadingUpdate(1, 1);
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
        var (service, licenseService, _, readerService, _) = await Setup(unitOfWork, context);

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
        await service.ScrobbleReadingUpdate(1, 1);
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
        await service.ScrobbleReadingUpdate(1, 1);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Equal(2, events.Count);
        Assert.Single(events.Where(e => e.IsProcessed).ToList());
        Assert.Single(events.Where(e => !e.IsProcessed).ToList());

        // Should update the existing non processed event
        await readerService.MarkChaptersAsRead(user, 1, [chapter3]);
        await unitOfWork.CommitAsync();

        // Scrobble update
        await service.ScrobbleReadingUpdate(1, 1);
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
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

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
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

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
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

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
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

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
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

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
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

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
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(false);

        await service.ScrobbleRatingUpdate(1, 1, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ScrobbleRatingUpdate_UpdateExistingNotIsProcessed()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (service, licenseService, _, _, _) = await Setup(unitOfWork, context);

        licenseService.HasActiveLicense().Returns(true);

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series);

        await service.ScrobbleRatingUpdate(user.Id, series.Id, 1);
        var events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);
        Assert.Equal(1, events.First().Rating);

        // Mark as processed
        events.First().IsProcessed = true;
        await unitOfWork.CommitAsync();

        await service.ScrobbleRatingUpdate(user.Id, series.Id, 5);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Equal(2, events.Count);
        Assert.Single(events, evt => evt.IsProcessed);
        Assert.Single(events, evt => !evt.IsProcessed);

        await service.ScrobbleRatingUpdate(user.Id, series.Id, 5);
        events = await unitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events, evt => !evt.IsProcessed);
        Assert.Equal(5, events.First(evt => !evt.IsProcessed).Rating);

    }

    #endregion

    [Theory]
    [InlineData("https://anilist.co/manga/35851/Byeontaega-Doeja/", 35851)]
    [InlineData("https://anilist.co/manga/30105", 30105)]
    [InlineData("https://anilist.co/manga/30105/Kekkaishi/", 30105)]
    public void CanParseWeblink_AniList(string link, int? expectedId)
    {
        Assert.Equal(ScrobblingService.ExtractId<int?>(link, ScrobblingService.AniListWeblinkWebsite), expectedId);
    }

    [Theory]
    [InlineData("https://mangadex.org/title/316d3d09-bb83-49da-9d90-11dc7ce40967/honzuki-no-gekokujou-shisho-ni-naru-tame-ni-wa-shudan-wo-erandeiraremasen-dai-3-bu-ryouchi-ni-hon-o", "316d3d09-bb83-49da-9d90-11dc7ce40967")]
    public void CanParseWeblink_MangaDex(string link, string expectedId)
    {
        Assert.Equal(ScrobblingService.ExtractId<string?>(link, ScrobblingService.MangaDexWeblinkWebsite), expectedId);
    }
}
