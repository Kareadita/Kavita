using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.Theme;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using AutoMapper;
using Kavita.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;


public abstract class SiteThemeServiceTest : AbstractDbTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly IEventHub _messageHub = Substitute.For<IEventHub>();


    protected SiteThemeServiceTest(ITestOutputHelper testOutputHelper) : base()
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
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.User,
            FileName = "custom.css",
            IsDefault = false
        });
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<KavitaException>(async () => await siteThemeService.UpdateDefault(10));
        Assert.Equal("Theme file missing or invalid", ex.Message);

    }

    [Fact]
    public async Task Scan_ShouldFindCustomFile()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[Scan_ShouldOnlyInsertOnceOnSecondScan] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);
        await siteThemeService.Scan();

        Assert.NotNull(await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("custom"));
    }

    [Fact]
    public async Task Scan_ShouldOnlyInsertOnceOnSecondScan()
    {
        await ResetDb();
        _testOutputHelper.WriteLine(
            $"[Scan_ShouldOnlyInsertOnceOnSecondScan] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);
        await siteThemeService.Scan();

        Assert.NotNull(await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("custom"));

        await siteThemeService.Scan();

        var customThemes = (await _unitOfWork.SiteThemeRepository.GetThemeDtos()).Where(t =>
            t.Name.ToNormalized().Equals("custom".ToNormalized()));

        Assert.Single(customThemes);
    }

    [Fact]
    public async Task Scan_ShouldDeleteWhenFileDoesntExistOnSecondScan()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[Scan_ShouldDeleteWhenFileDoesntExistOnSecondScan] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);
        await siteThemeService.Scan();

        Assert.NotNull(await _unitOfWork.SiteThemeRepository.GetThemeDtoByName("custom"));

        filesystem.RemoveFile($"{SiteThemeDirectory}custom.css");
        await siteThemeService.Scan();

        var themes = (await _unitOfWork.SiteThemeRepository.GetThemeDtos());

        Assert.Equal(0, themes.Count(t =>
            t.Name.ToNormalized().Equals("custom".ToNormalized())));
    }

    [Fact]
    public async Task GetContent_ShouldReturnContent()
    {
        await ResetDb();
        _testOutputHelper.WriteLine($"[GetContent_ShouldReturnContent] All Themes: {(await _unitOfWork.SiteThemeRepository.GetThemes()).Count(t => t.IsDefault)}");
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{SiteThemeDirectory}custom.css", new MockFileData("123"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.User,
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
        var siteThemeService = new ThemeService(ds, _unitOfWork, _messageHub);

        _context.SiteTheme.Add(new SiteTheme()
        {
            Name = "Custom",
            NormalizedName = "Custom".ToNormalized(),
            Provider = ThemeProvider.User,
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

