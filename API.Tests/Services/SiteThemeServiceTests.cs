using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums.Theme;
using API.Extensions;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using Kavita.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;


public abstract class SiteThemeServiceTest : AbstractDbTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly IEventHub _messageHub = Substitute.For<IEventHub>();


    protected SiteThemeServiceTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    protected override async Task ResetDb()
    {
        _context.SiteTheme.RemoveRange(_context.SiteTheme);
        await _context.SaveChangesAsync();
        // Recreate defaults
        await Seed.SeedThemes(_context);
    }

    [Fact]
    public async Task UpdateDefault_ShouldThrowOnInvalidId()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[UpdateDefault_ShouldThrowOnInvalidId] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub, Substitute.For<IFileService>(),
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IMemoryCache>());

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.Custom,
            FileName = "custom.css",
            IsDefault = false
        });
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<KavitaException>(() => siteThemeService.UpdateDefault(10));
        Assert.Equal("Theme file missing or invalid", ex.Message);

    }


    [Fact]
    public async Task GetContent_ShouldReturnContent()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[GetContent_ShouldReturnContent] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub, Substitute.For<IFileService>(),
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IMemoryCache>());

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.Custom,
            FileName = "custom.css",
            IsDefault = false
        });
        await _context.SaveChangesAsync();

        var content = await siteThemeService.GetContent((await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("Custom")).Id);
        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.Equal("123", content);
    }

    [Fact]
    public async Task UpdateDefault_ShouldHaveOneDefault()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[UpdateDefault_ShouldHaveOneDefault] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub, Substitute.For<IFileService>(),
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IMemoryCache>());

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.Custom,
            FileName = "custom.css",
            IsDefault = false
        });
        await _context.SaveChangesAsync();

        var customTheme = (await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("Custom"));

        Assert.NotNull(customTheme);
        await siteThemeService.UpdateDefault(customTheme.Id);



        Assert.Equal(customTheme.Id, (await _unitOfWork.SiteThemeRepository.GetDefaultTheme()).Id);
    }

}

