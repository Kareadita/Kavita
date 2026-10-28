using System.IO.Abstractions.TestingHelpers;
using Flurl.Http.Testing;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums.Theme;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;


public class SiteThemeServiceTest(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private readonly ITestOutputHelper _testOutputHelper = outputHelper;
    private readonly IEventHub _messageHub = Substitute.For<IEventHub>();

    [Fact]
    public async Task UpdateDefault_ShouldThrowOnInvalidId()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await Seed.SeedThemes(context);

        _testOutputHelper.WriteLine($"[UpdateDefault_ShouldThrowOnInvalidId] All Themes: {(await unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, unitOfWork, _messageHub,
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IGithubApiService>());

        context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.Custom,
            FileName = "custom.css",
            IsDefault = false
        });
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<KavitaException>(() => siteThemeService.UpdateDefault(10));
        Assert.Equal("theme-doesnt-exist", ex.Message);

    }


    [Fact]
    public async Task GetContent_ShouldReturnContent()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await Seed.SeedThemes(context);

        _testOutputHelper.WriteLine($"[GetContent_ShouldReturnContent] All Themes: {(await unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, unitOfWork, _messageHub,
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IGithubApiService>());

        context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.Custom,
            FileName = "custom.css",
            IsDefault = false
        });
        await context.SaveChangesAsync();

        var content = await siteThemeService.GetContent((await unitOfWork.SiteThemeRepository.GetThemeDtoByName("Custom")).Id);
        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.Equal("123", content);
    }

    [Fact]
    public async Task UpdateDefault_ShouldHaveOneDefault()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await Seed.SeedThemes(context);

        _testOutputHelper.WriteLine($"[UpdateDefault_ShouldHaveOneDefault] All Themes: {(await unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, unitOfWork, _messageHub,
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IGithubApiService>());

        context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.Custom,
            FileName = "custom.css",
            IsDefault = false
        });
        await context.SaveChangesAsync();

        var customTheme = (await unitOfWork.SiteThemeRepository.GetThemeDtoByName("Custom"));

        Assert.NotNull(customTheme);
        await siteThemeService.UpdateDefault(customTheme.Id);



        Assert.Equal(customTheme.Id, (await unitOfWork.SiteThemeRepository.GetDefaultTheme()).Id);
    }

    [Fact]
    public async Task BrowseAndSync_ShareOneGithubApiCall()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var themeNames = new[] { "Nord", "Dracula", "Light" };
        foreach (var name in themeNames)
        {
            context.SiteTheme.Add(new SiteTheme
            {
                Name = name,
                NormalizedName = name.ToNormalized(),
                Provider = ThemeProvider.Custom,
                FileName = $"{name}.css",
                GitHubPath = $"Native Themes/{name}",
                ShaHash = $"sha-{name}",
                CompatibleVersion = "0.1.0",
            });
        }
        await context.SaveChangesAsync();

        var entries = themeNames.SelectMany(name => new[]
        {
            new { path = $"Native Themes/{name}", type = "tree", sha = $"tree-{name}" },
            new { path = $"Native Themes/{name}/{name}.css", type = "blob", sha = $"sha-{name}" },
            new { path = $"Native Themes/{name}/preview.png", type = "blob", sha = $"png-{name}" },
        });

        var cachePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(cachePath);
        try
        {
            using var httpTest = new HttpTest();
            httpTest.ForCallsTo("https://api.github.com/*").RespondWithJson(new { tree = entries });
            httpTest.ForCallsTo("https://raw.githubusercontent.com/*").RespondWith("# Themes");

            var cacheDirectoryService = Substitute.For<IDirectoryService>();
            cacheDirectoryService.LongTermCacheDirectory.Returns(cachePath);
            var fileCacheService = new FileCacheService(cacheDirectoryService, Substitute.For<ILogger<FileCacheService>>());
            var githubApiService = new GithubApiService(fileCacheService, new MemoryCache(new MemoryCacheOptions()),
                Substitute.For<ILogger<GithubApiService>>());

            var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), CreateFileSystem());
            var siteThemeService = new ThemeService(ds, unitOfWork, _messageHub, Substitute.For<ILogger<ThemeService>>(), githubApiService);

            var browse = await siteThemeService.GetDownloadableThemes();
            await siteThemeService.SyncThemes();

            Assert.Equal(3, browse.Count);
            Assert.All(browse, t => Assert.True(t.AlreadyDownloaded));
            Assert.Single(httpTest.CallLog, c => c.Request.Url.ToString().StartsWith("https://api.github.com/"));
        }
        finally
        {
            Directory.Delete(cachePath, true);
        }
    }
}

