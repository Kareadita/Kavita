using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using API.DTOs.Update;
using API.Extensions;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using Flurl.Http;
using Flurl.Http.Testing;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class VersionUpdaterServiceTests : IDisposable
{
    private readonly ILogger<VersionUpdaterService> _logger;
    private readonly IEventHub _eventHub;
    private readonly IDirectoryService _directoryService;
    private readonly VersionUpdaterService _service;
    private readonly string _tempPath;
    private readonly HttpTest _httpTest;

    public VersionUpdaterServiceTests()
    {
        _logger = Substitute.For<ILogger<VersionUpdaterService>>();
        _eventHub = Substitute.For<IEventHub>();
        _directoryService = Substitute.For<IDirectoryService>();

        // Create temp directory for cache
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _directoryService.LongTermCacheDirectory.Returns(_tempPath);

        _service = new VersionUpdaterService(_logger, _eventHub, _directoryService);

        // Setup HTTP testing
        _httpTest = new HttpTest();

        // Mock BuildInfo.Version for consistent testing
        typeof(BuildInfo).GetProperty(nameof(BuildInfo.Version))?.SetValue(null, new Version("0.5.0.0"));
    }

    public void Dispose()
    {
        _httpTest.Dispose();

        // Cleanup temp directory
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }

        // Reset BuildInfo.Version
        typeof(BuildInfo).GetProperty(nameof(BuildInfo.Version))?.SetValue(null, null);
    }

    [Fact]
    public async Task CheckForUpdate_ShouldReturnNull_WhenGithubApiReturnsNull()
    {
        // Arrange
        _httpTest.RespondWith("null");

        // Act
        var result = await _service.CheckForUpdate();

        // Assert
        Assert.Null(result);
    }

    // Depends on BuildInfo.CurrentVersion
    //[Fact]
    public async Task CheckForUpdate_ShouldReturnUpdateNotification_WhenNewVersionIsAvailable()
    {
        // Arrange
        var githubResponse = new
        {
            tag_name = "v0.6.0",
            name = "Release 0.6.0",
            body = "# Added\n- Feature 1\n- Feature 2\n# Fixed\n- Bug 1\n- Bug 2",
            html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.6.0",
            published_at = DateTime.UtcNow.ToString("o")
        };

        _httpTest.RespondWithJson(githubResponse);

        // Act
        var result = await _service.CheckForUpdate();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("0.6.0", result.UpdateVersion);
        Assert.Equal("0.5.0.0", result.CurrentVersion);
        Assert.True(result.IsReleaseNewer);
        Assert.Equal(2, result.Added.Count);
        Assert.Equal(2, result.Fixed.Count);
    }

    //[Fact]
    public async Task CheckForUpdate_ShouldDetectEqualVersion()
    {
        // I can't figure this out
        typeof(BuildInfo).GetProperty(nameof(BuildInfo.Version))?.SetValue(null, new Version("0.5.0.0"));


        var githubResponse = new
        {
            tag_name = "v0.5.0",
            name = "Release 0.5.0",
            body = "# Added\n- Feature 1",
            html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.5.0",
            published_at = DateTime.UtcNow.ToString("o")
        };

        _httpTest.RespondWithJson(githubResponse);

        // Act
        var result = await _service.CheckForUpdate();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsReleaseEqual);
        Assert.False(result.IsReleaseNewer);
    }


    //[Fact]
    public async Task PushUpdate_ShouldSendUpdateEvent_WhenNewerVersionAvailable()
    {
        // Arrange
        var update = new UpdateNotificationDto
        {
            UpdateVersion = "0.6.0",
            CurrentVersion = "0.5.0.0",
            UpdateBody = "",
            UpdateTitle = null,
            UpdateUrl = null,
            PublishDate = null
        };

        // Act
        await _service.PushUpdate(update);

        // Assert
        await _eventHub.Received(1).SendMessageAsync(
            Arg.Is(MessageFactory.UpdateAvailable),
            Arg.Any<SignalRMessage>(),
            Arg.Is(true)
        );
    }

    [Fact]
    public async Task PushUpdate_ShouldNotSendUpdateEvent_WhenVersionIsEqual()
    {
        // Arrange
        var update = new UpdateNotificationDto
        {
            UpdateVersion = "0.5.0.0",
            CurrentVersion = "0.5.0.0",
            UpdateBody = "",
            UpdateTitle = null,
            UpdateUrl = null,
            PublishDate = null
        };

        // Act
        await _service.PushUpdate(update);

        // Assert
        await _eventHub.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(),
            Arg.Any<SignalRMessage>(),
            Arg.Any<bool>()
        );
    }

    [Fact]
    public async Task GetAllReleases_ShouldReturnReleases_LimitedByCount()
    {
        // Arrange
        var releases = new List<object>
        {
            new
            {
                tag_name = "v0.7.0",
                name = "Release 0.7.0",
                body = "# Added\n- Feature A",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.7.0",
                published_at = DateTime.UtcNow.AddDays(-1).ToString("o")
            },
            new
            {
                tag_name = "v0.6.0",
                name = "Release 0.6.0",
                body = "# Added\n- Feature B",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.6.0",
                published_at = DateTime.UtcNow.AddDays(-10).ToString("o")
            },
            new
            {
                tag_name = "v0.5.0",
                name = "Release 0.5.0",
                body = "# Added\n- Feature C",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.5.0",
                published_at = DateTime.UtcNow.AddDays(-20).ToString("o")
            }
        };

        _httpTest.RespondWithJson(releases);

        // Act
        var result = await _service.GetAllReleases(2);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("0.7.0.0", result[0].UpdateVersion);
        Assert.Equal("0.6.0", result[1].UpdateVersion);
    }

    [Fact]
    public async Task GetAllReleases_ShouldUseCachedData_WhenCacheIsValid()
    {
        // Arrange
        var releases = new List<UpdateNotificationDto>
        {
            new()
            {
                UpdateVersion = "0.6.0",
                CurrentVersion = "0.5.0.0",
                PublishDate = DateTime.UtcNow.AddDays(-10)
                    .ToString("o"),
                UpdateBody = "",
                UpdateTitle = null,
                UpdateUrl = null
            }
        };
        releases.Add(new()
        {
            UpdateVersion = "0.7.0",
            CurrentVersion = "0.5.0.0",
            PublishDate = DateTime.UtcNow.AddDays(-1)
                .ToString("o"),
            UpdateBody = "",
            UpdateTitle = null,
            UpdateUrl = null
        });

        // Create cache file
        var cacheFilePath = Path.Combine(_tempPath, "github_releases_cache.json");
        await File.WriteAllTextAsync(cacheFilePath, System.Text.Json.JsonSerializer.Serialize(releases));
        File.SetLastWriteTimeUtc(cacheFilePath, DateTime.UtcNow); // Ensure it's fresh

        // Act
        var result = await _service.GetAllReleases();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Empty(_httpTest.CallLog); // No HTTP calls made
    }

    [Fact]
    public async Task GetAllReleases_ShouldFetchNewData_WhenCacheIsExpired()
    {
        // Arrange
        var releases = new List<UpdateNotificationDto>
        {
            new()
            {
                UpdateVersion = "0.6.0",
                CurrentVersion = "0.5.0.0",
                PublishDate = DateTime.UtcNow.AddDays(-10)
                    .ToString("o"),
                UpdateBody = null,
                UpdateTitle = null,
                UpdateUrl = null
            }
        };

        // Create expired cache file
        var cacheFilePath = Path.Combine(_tempPath, "github_releases_cache.json");
        await File.WriteAllTextAsync(cacheFilePath, System.Text.Json.JsonSerializer.Serialize(releases));
        File.SetLastWriteTimeUtc(cacheFilePath, DateTime.UtcNow.AddHours(-2)); // Expired (older than 1 hour)

        // Setup HTTP response for new fetch
        var newReleases = new List<object>
        {
            new
            {
                tag_name = "v0.7.0",
                name = "Release 0.7.0",
                body = "# Added\n- Feature A",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.7.0",
                published_at = DateTime.UtcNow.ToString("o")
            }
        };

        _httpTest.RespondWithJson(newReleases);

        // Act
        var result = await _service.GetAllReleases();

        // Assert
        Assert.Equal(1, result.Count);
        Assert.Equal("0.7.0.0", result[0].UpdateVersion);
        Assert.NotEmpty(_httpTest.CallLog); // HTTP call was made
    }

    public async Task GetNumberOfReleasesBehind_ShouldReturnCorrectCount()
    {
        // Arrange
        var releases = new List<object>
        {
            new
            {
                tag_name = "v0.7.0",
                name = "Release 0.7.0",
                body = "# Added\n- Feature A",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.7.0",
                published_at = DateTime.UtcNow.AddDays(-1).ToString("o")
            },
            new
            {
                tag_name = "v0.6.0",
                name = "Release 0.6.0",
                body = "# Added\n- Feature B",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.6.0",
                published_at = DateTime.UtcNow.AddDays(-10).ToString("o")
            },
            new
            {
                tag_name = "v0.5.0",
                name = "Release 0.5.0",
                body = "# Added\n- Feature C",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.5.0",
                published_at = DateTime.UtcNow.AddDays(-20).ToString("o")
            }
        };

        _httpTest.RespondWithJson(releases);

        // Act
        var result = await _service.GetNumberOfReleasesBehind();

        // Assert
        Assert.Equal(2 + 1, result); // Behind 0.7.0 and 0.6.0  - We have to add 1 because the current release is > 0.7.0
    }

    public async Task GetNumberOfReleasesBehind_ShouldReturnCorrectCount_WithNightlies()
    {
        // Arrange
        var releases = new List<object>
        {
            new
            {
                tag_name = "v0.7.1",
                name = "Release 0.7.1",
                body = "# Added\n- Feature A",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.7.1",
                published_at = DateTime.UtcNow.AddDays(-1).ToString("o")
            },
            new
            {
                tag_name = "v0.7.0",
                name = "Release 0.7.0",
                body = "# Added\n- Feature A",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.7.0",
                published_at = DateTime.UtcNow.AddDays(-10).ToString("o")
            },
        };

        _httpTest.RespondWithJson(releases);

        // Act
        var result = await _service.GetNumberOfReleasesBehind();

        // Assert
        Assert.Equal(2, result); //  We have to add 1 because the current release is > 0.7.0
    }

    [Fact]
    public async Task ParseReleaseBody_ShouldExtractSections()
    {
        // Arrange
        var githubResponse = new
        {
            tag_name = "v0.6.0",
            name = "Release 0.6.0",
            body = "This is a great release with many improvements!\n\n# Added\n- Feature 1\n- Feature 2\n# Fixed\n- Bug 1\n- Bug 2\n# Changed\n- Change 1\n# Developer\n- Dev note 1",
            html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.6.0",
            published_at = DateTime.UtcNow.ToString("o")
        };

        _httpTest.RespondWithJson(githubResponse);

        // Act
        var result = await _service.CheckForUpdate();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Added.Count);
        Assert.Equal(2, result.Fixed.Count);
        Assert.Equal(1, result.Changed.Count);
        Assert.Equal(1, result.Developer.Count);
        Assert.Contains("This is a great release", result.BlogPart);
    }

    [Fact]
    public async Task GetAllReleases_ShouldHandleNightlyBuilds()
    {
        // Arrange
        // Set BuildInfo.Version to a nightly build version
        typeof(BuildInfo).GetProperty(nameof(BuildInfo.Version))?.SetValue(null, new Version("0.7.1.0"));

        // Mock regular releases
        var releases = new List<object>
        {
            new
            {
                tag_name = "v0.7.0",
                name = "Release 0.7.0",
                body = "# Added\n- Feature A",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.7.0",
                published_at = DateTime.UtcNow.AddDays(-1).ToString("o")
            },
            new
            {
                tag_name = "v0.6.0",
                name = "Release 0.6.0",
                body = "# Added\n- Feature B",
                html_url = "https://github.com/Kareadita/Kavita/releases/tag/v0.6.0",
                published_at = DateTime.UtcNow.AddDays(-10).ToString("o")
            }
        };

        _httpTest.RespondWithJson(releases);

        // Mock commit info for develop branch
        _httpTest.RespondWithJson(new List<object>());

        // Act
        var result = await _service.GetAllReleases();

        // Assert
        Assert.NotNull(result);
        Assert.True(result[0].IsOnNightlyInRelease);
    }
}
