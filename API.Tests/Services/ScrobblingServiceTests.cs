using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Scrobble;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;
#nullable enable

public class ScrobblingServiceTests : AbstractDbTest
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

    private readonly ScrobblingService _service;
    private readonly ILicenseService _licenseService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<ScrobblingService> _logger;
    private readonly IEmailService _emailService;
    private readonly IKavitaPlusApiService _kavitaPlusApiService;
    /// <summary>
    /// IReaderService, without the ScrobblingService injected
    /// </summary>
    private readonly IReaderService _readerService;
    /// <summary>
    /// IReaderService, with the _service injected
    /// </summary>
    private readonly IReaderService _hookedUpReaderService;

    public ScrobblingServiceTests()
    {
        _licenseService = Substitute.For<ILicenseService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _logger = Substitute.For<ILogger<ScrobblingService>>();
        _emailService = Substitute.For<IEmailService>();
        _kavitaPlusApiService = Substitute.For<IKavitaPlusApiService>();

        _service = new ScrobblingService(UnitOfWork, Substitute.For<IEventHub>(), _logger,  _licenseService,
            _localizationService, _emailService, _kavitaPlusApiService);

        _readerService = new ReaderService(UnitOfWork,
            Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(),
            Substitute.For<IScrobblingService>()); // Do not use the actual one

        _hookedUpReaderService = new ReaderService(UnitOfWork,
            Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(),
            _service);
    }

    protected override async Task ResetDb()
    {
        Context.ScrobbleEvent.RemoveRange(Context.ScrobbleEvent.ToList());
        Context.Series.RemoveRange(Context.Series.ToList());
        Context.Library.RemoveRange(Context.Library.ToList());
        Context.AppUser.RemoveRange(Context.AppUser.ToList());

        await UnitOfWork.CommitAsync();
    }

    private async Task SeedData()
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


        Context.Library.Add(library);

        var user = new AppUserBuilder("testuser", "testuser")
            //.WithPreferences(new UserPreferencesBuilder().WithAniListScrobblingEnabled(true).Build())
            .Build();

        user.UserPreferences.AniListScrobblingEnabled = true;

        UnitOfWork.UserRepository.Add(user);

        await UnitOfWork.CommitAsync();
    }

    private async Task<ScrobbleEvent> CreateScrobbleEvent(int? seriesId = null)
    {
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
            var series = await UnitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId.Value);
            if (series != null) evt.Series = series;
        }

        return evt;
    }


    #region K+ API Request Tests

    [Fact]
    public async Task PostScrobbleUpdate_AuthErrors()
    {
        _kavitaPlusApiService.PostScrobbleUpdate(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Unauthorized"
            });

        var evt = await CreateScrobbleEvent();
        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await _service.PostScrobbleUpdate(new ScrobbleDto(), "", evt);
        });
        Assert.True(evt.IsErrored);
        Assert.Equal("Kavita+ subscription no longer active", evt.ErrorDetails);
    }

    [Fact]
    public async Task PostScrobbleUpdate_UnknownSeriesLoggedAsError()
    {
        _kavitaPlusApiService.PostScrobbleUpdate(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Unknown Series"
            });

        await SeedData();
        var evt = await CreateScrobbleEvent(1);

        await _service.PostScrobbleUpdate(new ScrobbleDto(), "", evt);
        await UnitOfWork.CommitAsync();
        Assert.True(evt.IsErrored);

        var series = await UnitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series);
        Assert.True(series.IsBlacklisted);

        var errors = await UnitOfWork.ScrobbleRepository.GetAllScrobbleErrorsForSeries(1);
        Assert.Single(errors);
        Assert.Equal("Series cannot be matched for Scrobbling", errors.First().Comment);
        Assert.Equal(series.Id, errors.First().SeriesId);
    }

    [Fact]
    public async Task PostScrobbleUpdate_InvalidAccessToken()
    {
        _kavitaPlusApiService.PostScrobbleUpdate(null!, "")
            .ReturnsForAnyArgs(new ScrobbleResponseDto()
            {
                ErrorMessage = "Access token is invalid"
            });

        var evt = await CreateScrobbleEvent();

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await _service.PostScrobbleUpdate(new ScrobbleDto(), "", evt);
        });

        Assert.True(evt.IsErrored);
        Assert.Equal("Access Token needs to be rotated to continue scrobbling", evt.ErrorDetails);
    }

    #endregion

    #region K+ API Request data tests

    [Fact]
    public async Task ProcessReadEvents_CreatesNoEventsWhenNoProgress()
    {
        await ResetDb();
        await SeedData();

        // Set Returns
        _licenseService.HasActiveLicense().Returns(Task.FromResult(true));
        _kavitaPlusApiService.GetRateLimit(Arg.Any<string>(), Arg.Any<string>())
            .Returns(100);

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        // Ensure CanProcessScrobbleEvent returns true
        user.AniListAccessToken = ValidJwtToken;
        UnitOfWork.UserRepository.Update(user);
        await UnitOfWork.CommitAsync();

        var chapter = await UnitOfWork.ChapterRepository.GetChapterAsync(4);
        Assert.NotNull(chapter);

        var volume = await UnitOfWork.VolumeRepository.GetVolumeAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        // Call Scrobble without having any progress
        await _service.ScrobbleReadingUpdate(1, 1);
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ProcessReadEvents_UpdateVolumeAndChapterData()
    {
        await ResetDb();
        await SeedData();

        // Set Returns
        _licenseService.HasActiveLicense().Returns(Task.FromResult(true));
        _kavitaPlusApiService.GetRateLimit(Arg.Any<string>(), Arg.Any<string>())
            .Returns(100);

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        // Ensure CanProcessScrobbleEvent returns true
        user.AniListAccessToken = ValidJwtToken;
        UnitOfWork.UserRepository.Update(user);
        await UnitOfWork.CommitAsync();

        var chapter = await UnitOfWork.ChapterRepository.GetChapterAsync(4);
        Assert.NotNull(chapter);

        var volume = await UnitOfWork.VolumeRepository.GetVolumeAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        // Mark something as read to trigger event creation
        await _readerService.MarkChaptersAsRead(user, 1, new List<Chapter>() {volume.Chapters[0]});
        await UnitOfWork.CommitAsync();

        // Call Scrobble while having some progress
        await _service.ScrobbleReadingUpdate(user.Id, 1);
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);

        // Give it some (more) read progress
        await _readerService.MarkChaptersAsRead(user, 1, volume.Chapters);
        await _readerService.MarkChaptersAsRead(user, 1, [chapter]);
        await UnitOfWork.CommitAsync();

        await _service.ProcessUpdatesSinceLastSync();

        await _kavitaPlusApiService.Received(1).PostScrobbleUpdate(
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
        await ResetDb();
        await SeedData();

        _licenseService.HasActiveLicense().Returns(false);

        await _service.ScrobbleReadingUpdate(1, 1);
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ScrobbleReadingUpdate_RemoveWhenNoProgress()
    {
        await ResetDb();
        await SeedData();

        _licenseService.HasActiveLicense().Returns(true);

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var volume = await UnitOfWork.VolumeRepository.GetVolumeAsync(1, VolumeIncludes.Chapters);
        Assert.NotNull(volume);

        await _readerService.MarkChaptersAsRead(user, 1, new List<Chapter>() {volume.Chapters[0]});
        await UnitOfWork.CommitAsync();

        await _service.ScrobbleReadingUpdate(1, 1);
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);

        var readEvent = events.First();
        Assert.False(readEvent.IsProcessed);

        await _hookedUpReaderService.MarkSeriesAsUnread(user, 1);
        await UnitOfWork.CommitAsync();

        // Existing event is deleted
        await _service.ScrobbleReadingUpdate(1, 1);
        events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);

        await _hookedUpReaderService.MarkSeriesAsUnread(user, 1);
        await UnitOfWork.CommitAsync();

        // No new events are added
        events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ScrobbleReadingUpdate_UpdateExistingNotIsProcessed()
    {
        await ResetDb();
        await SeedData();

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var chapter1 = await UnitOfWork.ChapterRepository.GetChapterAsync(1);
        var chapter2 = await UnitOfWork.ChapterRepository.GetChapterAsync(2);
        var chapter3 = await UnitOfWork.ChapterRepository.GetChapterAsync(3);
        Assert.NotNull(chapter1);
        Assert.NotNull(chapter2);
        Assert.NotNull(chapter3);

        _licenseService.HasActiveLicense().Returns(true);

        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);


        await _readerService.MarkChaptersAsRead(user, 1, [chapter1]);
        await UnitOfWork.CommitAsync();

        // Scrobble update
        await _service.ScrobbleReadingUpdate(1, 1);
        events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);

        var readEvent = events[0];
        Assert.False(readEvent.IsProcessed);
        Assert.Equal(1, readEvent.ChapterNumber);

        // Mark as processed
        readEvent.IsProcessed = true;
        await UnitOfWork.CommitAsync();

        await _readerService.MarkChaptersAsRead(user, 1, [chapter2]);
        await UnitOfWork.CommitAsync();

        // Scrobble update
        await _service.ScrobbleReadingUpdate(1, 1);
        events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Equal(2, events.Count);
        Assert.Single(events.Where(e => e.IsProcessed).ToList());
        Assert.Single(events.Where(e => !e.IsProcessed).ToList());

        // Should update the existing non processed event
        await _readerService.MarkChaptersAsRead(user, 1, [chapter3]);
        await UnitOfWork.CommitAsync();

        // Scrobble update
        await _service.ScrobbleReadingUpdate(1, 1);
        events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Equal(2, events.Count);
        Assert.Single(events.Where(e => e.IsProcessed).ToList());
        Assert.Single(events.Where(e => !e.IsProcessed).ToList());
    }

    #endregion

    #region ScrobbleWantToReadUpdate Tests

    [Fact]
    public async Task ScrobbleWantToReadUpdate_NoExistingEvents_WantToRead_ShouldCreateNewEvent()
    {
        // Arrange
        await SeedData();
        _licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // Act
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Assert
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.AddWantToRead, events[0].ScrobbleEventType);
        Assert.Equal(userId, events[0].AppUserId);
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_NoExistingEvents_RemoveWantToRead_ShouldCreateNewEvent()
    {
        // Arrange
        await SeedData();
        _licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // Act
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Assert
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);
        Assert.Single(events);
        Assert.Equal(ScrobbleEventType.RemoveWantToRead, events[0].ScrobbleEventType);
        Assert.Equal(userId, events[0].AppUserId);
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_ExistingWantToReadEvent_WantToRead_ShouldNotCreateNewEvent()
    {
        // Arrange
        await SeedData();
        _licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // First, let's create an event through the service
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Act - Try to create the same event again
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Assert
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);

        Assert.Single(events);
        Assert.All(events, e => Assert.Equal(ScrobbleEventType.AddWantToRead, e.ScrobbleEventType));
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_ExistingWantToReadEvent_RemoveWantToRead_ShouldAddRemoveEvent()
    {
        // Arrange
        await SeedData();
        _licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // First, let's create a want-to-read event through the service
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Act - Now remove from want-to-read
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Assert
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);

        Assert.Single(events);
        Assert.Contains(events, e => e.ScrobbleEventType == ScrobbleEventType.RemoveWantToRead);
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_ExistingRemoveWantToReadEvent_RemoveWantToRead_ShouldNotCreateNewEvent()
    {
        // Arrange
        await SeedData();
        _licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // First, let's create a remove-from-want-to-read event through the service
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Act - Try to create the same event again
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Assert
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);

        Assert.Single(events);
        Assert.All(events, e => Assert.Equal(ScrobbleEventType.RemoveWantToRead, e.ScrobbleEventType));
    }

    [Fact]
    public async Task ScrobbleWantToReadUpdate_ExistingRemoveWantToReadEvent_WantToRead_ShouldAddWantToReadEvent()
    {
        // Arrange
        await SeedData();
        _licenseService.HasActiveLicense().Returns(Task.FromResult(true));

        const int userId = 1;
        const int seriesId = 1;

        // First, let's create a remove-from-want-to-read event through the service
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, false);

        // Act - Now add to want-to-read
        await _service.ScrobbleWantToReadUpdate(userId, seriesId, true);

        // Assert
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(seriesId);

        Assert.Single(events);
        Assert.Contains(events, e => e.ScrobbleEventType == ScrobbleEventType.AddWantToRead);
    }

    #endregion

    #region Scrobble Rating Update Test

    [Fact]
    public async Task ScrobbleRatingUpdate_IgnoreNoLicense()
    {
        await ResetDb();
        await SeedData();

        _licenseService.HasActiveLicense().Returns(false);

        await _service.ScrobbleRatingUpdate(1, 1, 1);
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ScrobbleRatingUpdate_UpdateExistingNotIsProcessed()
    {
        await ResetDb();
        await SeedData();

        _licenseService.HasActiveLicense().Returns(true);

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1);
        Assert.NotNull(user);

        var series = await UnitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series);

        await _service.ScrobbleRatingUpdate(user.Id, series.Id, 1);
        var events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Single(events);
        Assert.Equal(1, events.First().Rating);

        // Mark as processed
        events.First().IsProcessed = true;
        await UnitOfWork.CommitAsync();

        await _service.ScrobbleRatingUpdate(user.Id, series.Id, 5);
        events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
        Assert.Equal(2, events.Count);
        Assert.Single(events, evt => evt.IsProcessed);
        Assert.Single(events, evt => !evt.IsProcessed);

        await _service.ScrobbleRatingUpdate(user.Id, series.Id, 5);
        events = await UnitOfWork.ScrobbleRepository.GetAllEventsForSeries(1);
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
