using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Scrobbling;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;
#nullable enable

public class ScrobblingServiceTests : AbstractDbTest
{
    private readonly ScrobblingService _service;
    private readonly ILicenseService _licenseService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<ScrobblingService> _logger;
    private readonly IEmailService _emailService;

    public ScrobblingServiceTests()
    {
        _licenseService = Substitute.For<ILicenseService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _logger = Substitute.For<ILogger<ScrobblingService>>();
        _emailService = Substitute.For<IEmailService>();

        _service = new ScrobblingService(UnitOfWork, Substitute.For<IEventHub>(), _logger,  _licenseService, _localizationService, _emailService);
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
