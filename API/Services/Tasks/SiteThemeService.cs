using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Theme;
using API.Entities;
using API.Entities.Enums.Theme;
using API.Extensions;
using API.SignalR;
using Flurl.Http;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace API.Services.Tasks;
#nullable enable

public interface IThemeService
{
    Task<string> GetContent(int themeId);
    Task Scan();
    Task UpdateDefault(int themeId);
    /// <summary>
    /// Browse theme repo for themes to download
    /// </summary>
    /// <returns></returns>
    Task<List<DownloadableSiteThemeDto>> BrowseRepoThemes();

    Task<SiteTheme> DownloadRepoTheme(DownloadableSiteThemeDto dto);
}

internal class GitHubContent
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("download_url")]
    public string DownloadUrl { get; set; }

    [JsonProperty("sha")]
    public string Sha { get; set; }
}


public class ThemeService : IThemeService
{
    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly IFileService _fileService;
    private readonly ILogger<ThemeService> _logger;

    private readonly string _githubBaseUrl = "https://api.github.com";
    /// <summary>
    /// Used for refreshing metadata around themes
    /// </summary>
    private readonly string _githubReadme = $"https://api.github.com/repos/Kareadita/Themes/contents/README.md";

    public ThemeService(IDirectoryService directoryService, IUnitOfWork unitOfWork,
        IEventHub eventHub, IFileService fileService, ILogger<ThemeService> logger)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Given a themeId, return the content inside that file
    /// </summary>
    /// <param name="themeId"></param>
    /// <returns></returns>
    public async Task<string> GetContent(int themeId)
    {
        var theme = await _unitOfWork.SiteThemeRepository.GetThemeDto(themeId);
        if (theme == null) throw new KavitaException("theme-doesnt-exist");
        var themeFile = _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, theme.FileName);
        if (string.IsNullOrEmpty(themeFile) || !_directoryService.FileSystem.File.Exists(themeFile))
            throw new KavitaException("theme-doesnt-exist");

        return await _directoryService.FileSystem.File.ReadAllTextAsync(themeFile);
    }

    public async Task<List<DownloadableSiteThemeDto>> BrowseRepoThemes()
    {
        // Fetch contents of the Native Themes directory
        var themesContents = await GetDirectoryContent("Native%20Themes");

        // Filter out directories
        var themeDirectories = themesContents.Where(c => c.Type == "dir").ToList();

        var themeDtos = new List<DownloadableSiteThemeDto>();

        foreach (var themeDir in themeDirectories)
        {
            var themeName = themeDir.Name;

            // Fetch contents of the theme directory
            var themeContents = await GetDirectoryContent(themeDir.Path);

            // Find css and preview files
            var cssFile = themeContents.FirstOrDefault(c => c.Name.EndsWith(".css"));
            var previewFile = themeContents.FirstOrDefault(c => c.Name.ToLower().Contains("preview.jpg"));

            if (cssFile != null && previewFile != null)
            {
                var cssUrl = cssFile.DownloadUrl;
                var previewUrl = previewFile.DownloadUrl;

                themeDtos.Add(new DownloadableSiteThemeDto()
                {
                    Name = themeName,
                    CssUrl = cssUrl,
                    CssFile = cssFile.Name,
                    PreviewUrl = previewUrl,
                    Sha = cssFile.Sha,
                    Path = cssFile.Path
                });
            }
        }

        return themeDtos;
    }

    private async Task<IList<GitHubContent>> GetDirectoryContent(string path)
    {
        return await $"{_githubBaseUrl}/repos/Kareadita/Themes/contents/{path}"
            .WithHeader("Accept", "application/vnd.github+json")
            .WithHeader("User-Agent", "Kavita")
            .GetJsonAsync<List<GitHubContent>>();
    }


    private async Task<string> DownloadSiteTheme(DownloadableSiteThemeDto dto)
    {
        if (string.IsNullOrEmpty(dto.Sha))
        {
            throw new ArgumentException("SHA cannot be null or empty for already downloaded themes.");
        }

        var tempDownloadFile = await dto.CssUrl.DownloadFileAsync(_directoryService.TempDirectory);

        // Validate the hash on the downloaded file
        // if (!_fileService.ValidateSha(tempDownloadFile, dto.Sha))
        // {
        //     throw new KavitaException("Cannot download theme, hash does not match");
        // }

        _directoryService.CopyFileToDirectory(tempDownloadFile, _directoryService.SiteThemeDirectory);
        var finalLocation = _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, dto.CssFile);

        return finalLocation;
    }


    public async Task<SiteTheme> DownloadRepoTheme(DownloadableSiteThemeDto dto)
    {

        // Validate we don't have a collision with existing or existing doesn't already exist
        var existingThemes = _directoryService.ScanFiles(_directoryService.SiteThemeDirectory, "");
        if (existingThemes.Any(f => Path.GetFileName(f) == dto.CssFile))
        {
            throw new KavitaException("Cannot download file, file already on disk");
        }

        var finalLocation = await DownloadSiteTheme(dto);

        // Create a new entry and note that this is downloaded
        var theme = new SiteTheme()
        {
            Name = dto.Name,
            NormalizedName = dto.Name.ToNormalized(),
            FileName = _directoryService.FileSystem.Path.GetFileName(finalLocation),
            Provider = ThemeProvider.Downloaded,
            IsDefault = false,
            GitHubPath = dto.Path,
        };
        _unitOfWork.SiteThemeRepository.Add(theme);

        await _unitOfWork.CommitAsync();

        return theme;
    }

    // public async Task CreateTheme(string fileInTemp)
    // {
    //
    //     // temp css file,
    // }

    public async Task SyncTheme(SiteTheme? theme)
    {
        // Given a theme, first validate that it is applicable
        if (theme == null || theme.Provider != ThemeProvider.Downloaded || string.IsNullOrEmpty(theme.GitHubPath))
        {
            _logger.LogInformation("Cannot Sync theme as it is not valid");
            return;
        }

        //theme.GitHubPath
        var themeContents = await GetDirectoryContent(theme.GitHubPath);
        var cssFile = themeContents.FirstOrDefault(c => c.Name.EndsWith(".css"));

        if (cssFile == null) return;
        if (cssFile.Sha == theme.ShaHash)
        {
            _logger.LogInformation("Theme {ThemeName} is up to date", theme.Name);
            // TODO: I might want to refresh data from the Readme
            return;
        }

        var location = _directoryService.FileSystem.Path.Join(_directoryService.TempDirectory, theme.FileName);

    }

    /// <summary>
    /// Deletes a SiteTheme. The CSS file will be moved to temp/ to allow user to recover data
    /// </summary>
    /// <param name="siteThemeId"></param>
    public async Task DeleteTheme(int siteThemeId)
    {
        var siteTheme = await _unitOfWork.SiteThemeRepository.GetTheme(siteThemeId);
        if (siteTheme == null) return;

        await RemoveTheme(siteTheme);
    }

    /// <summary>
    /// Scans the site theme directory for custom css files and updates what the system has on store
    /// </summary>
    public async Task Scan()
    {
        _directoryService.ExistOrCreate(_directoryService.SiteThemeDirectory);
        var reservedNames = Seed.DefaultThemes.Select(t => t.NormalizedName).ToList();
        var themeFiles = _directoryService
            .GetFilesWithExtension(Scanner.Parser.Parser.NormalizePath(_directoryService.SiteThemeDirectory), @"\.css")
            .Where(name => !reservedNames.Contains(name.ToNormalized()) && !name.Contains(' '))
            .ToList();

        var allThemes = (await _unitOfWork.SiteThemeRepository.GetThemes()).ToList();

        // First remove any files from allThemes that are User Defined and not on disk
        var userThemes = allThemes.Where(t => t.Provider == ThemeProvider.User).ToList();
        foreach (var userTheme in userThemes)
        {
            var filepath = Scanner.Parser.Parser.NormalizePath(
                _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, userTheme.FileName));
            if (_directoryService.FileSystem.File.Exists(filepath)) continue;

            // I need to do the removal different. I need to update all user preferences to use DefaultTheme
            allThemes.Remove(userTheme);
            await RemoveTheme(userTheme);
        }

        // Add new custom themes
        var allThemeNames = allThemes.Select(t => t.NormalizedName).ToList();
        foreach (var themeFile in themeFiles)
        {
            var themeName =
                _directoryService.FileSystem.Path.GetFileNameWithoutExtension(themeFile).ToNormalized();
            if (allThemeNames.Contains(themeName)) continue;

            _unitOfWork.SiteThemeRepository.Add(new SiteTheme()
            {
                Name = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(themeFile),
                NormalizedName = themeName,
                FileName = _directoryService.FileSystem.Path.GetFileName(themeFile),
                Provider = ThemeProvider.User,
                IsDefault = false,
            });

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.SiteThemeProgressEvent(_directoryService.FileSystem.Path.GetFileName(themeFile), themeName,
                    ProgressEventType.Updated));
        }


        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        // if there are no default themes, reselect Dark as default
        var postSaveThemes = (await _unitOfWork.SiteThemeRepository.GetThemes()).ToList();
        if (!postSaveThemes.Exists(t => t.IsDefault))
        {
            var defaultThemeName = Seed.DefaultThemes.Single(t => t.IsDefault).NormalizedName;
            var theme = postSaveThemes.SingleOrDefault(t => t.NormalizedName == defaultThemeName);
            if (theme != null)
            {
                theme.IsDefault = true;
                _unitOfWork.SiteThemeRepository.Update(theme);
                await _unitOfWork.CommitAsync();
            }

        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.SiteThemeProgressEvent("", "", ProgressEventType.Ended));
    }


    /// <summary>
    /// Removes the theme and any references to it from Pref and sets them to the default at the time.
    /// This commits to DB.
    /// </summary>
    /// <param name="theme"></param>
    private async Task RemoveTheme(SiteTheme theme)
    {
        _logger.LogInformation("Removing {ThemeName}. File can be found in temp/ until nightly cleanup", theme.Name);
        var prefs = await _unitOfWork.UserRepository.GetAllPreferencesByThemeAsync(theme.Id);
        var defaultTheme = await _unitOfWork.SiteThemeRepository.GetDefaultTheme();
        foreach (var pref in prefs)
        {
            pref.Theme = defaultTheme;
            _unitOfWork.UserRepository.Update(pref);
        }

        try
        {
            // Copy the theme file to temp for nightly removal (to give user time to reclaim if made a mistake)
            var existingLocation =
                _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, theme.FileName);
            var newLocation =
                _directoryService.FileSystem.Path.Join(_directoryService.TempDirectory, theme.FileName);
            _directoryService.CopyFileToDirectory(existingLocation, newLocation);
        }
        catch (Exception) { /* Swallow */ }


        _unitOfWork.SiteThemeRepository.Remove(theme);
        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Updates the themeId to the default theme, all others are marked as non-default
    /// </summary>
    /// <param name="themeId"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException">If theme does not exist</exception>
    public async Task UpdateDefault(int themeId)
    {
        try
        {
            var theme = await _unitOfWork.SiteThemeRepository.GetThemeDto(themeId);
            if (theme == null) throw new KavitaException("theme-doesnt-exist");

            foreach (var siteTheme in await _unitOfWork.SiteThemeRepository.GetThemes())
            {
                siteTheme.IsDefault = (siteTheme.Id == themeId);
                _unitOfWork.SiteThemeRepository.Update(siteTheme);
            }

            if (!_unitOfWork.HasChanges()) return;
            await _unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Returns the naming convention for cover images for SiteThemes
    /// </summary>
    /// <param name="siteThemeId"></param>
    /// <returns></returns>
    public static string SiteThemeFormat(int siteThemeId)
    {
        return $"theme_{siteThemeId}";
    }
}
