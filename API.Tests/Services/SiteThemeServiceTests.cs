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
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IMemoryCache>());

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
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IMemoryCache>());

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
            Substitute.For<ILogger<ThemeService>>(), Substitute.For<IMemoryCache>());

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

}

