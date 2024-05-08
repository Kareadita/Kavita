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
using HtmlAgilityPack;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using MarkdownDeep;
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

/// <summary>
/// The readme of the Theme repo
/// </summary>
internal class ThemeMetadata
{
    public string Author { get; set; }
    public string AuthorUrl { get; set; }
    public string Description { get; set; }
    public Version LastCompatible { get; set; }
}


public class ThemeService : IThemeService
{
    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly IFileService _fileService;
    private readonly ILogger<ThemeService> _logger;
    private readonly Markdown _markdown = new MarkdownDeep.Markdown();

    private readonly string _githubBaseUrl = "https://api.github.com";
    /// <summary>
    /// Used for refreshing metadata around themes
    /// </summary>
    private readonly string _githubReadme = "https://raw.githubusercontent.com/Kareadita/Themes/main/README.md";

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

        var existingThemes = (await _unitOfWork.SiteThemeRepository.GetThemeDtos()).ToDictionary(k => k.Name);



        // Filter out directories
        var themeDirectories = themesContents.Where(c => c.Type == "dir").ToList();

        // Get the Readme and augment the theme data
        var themeMetadata = await GetReadme();

        var themeDtos = new List<DownloadableSiteThemeDto>();
        foreach (var themeDir in themeDirectories)
        {
            var themeName = themeDir.Name.Trim();

            // Fetch contents of the theme directory
            var themeContents = await GetDirectoryContent(themeDir.Path);

            // Find css and preview files
            var cssFile = themeContents.FirstOrDefault(c => c.Name.EndsWith(".css"));
            var previewFiles = themeContents.Where(c => c.Name.ToLower().EndsWith(".jpg"));

            if (cssFile == null) continue;

            var cssUrl = cssFile.DownloadUrl;


            var dto = new DownloadableSiteThemeDto()
            {
                Name = themeName,
                CssUrl = cssUrl,
                CssFile = cssFile.Name,
                PreviewUrls = previewFiles.Select(p => p.DownloadUrl).ToList(),
                Sha = cssFile.Sha,
                Path = cssFile.Path,
            };

            if (themeMetadata.TryGetValue(themeName, out var metadata))
            {
                dto.Author = metadata.Author;
                dto.LastCompatibleVersion = metadata.LastCompatible.ToString();
                dto.IsCompatible = BuildInfo.Version <= metadata.LastCompatible;
                dto.AlreadyDownloaded = existingThemes.ContainsKey(themeName);
            }

            themeDtos.Add(dto);
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

    /// <summary>
    /// Returns a map of all Native Themes names mapped to their metadata
    /// </summary>
    /// <returns></returns>
    private async Task<IDictionary<string, ThemeMetadata>> GetReadme()
    {
        var tempDownloadFile = await _githubReadme.DownloadFileAsync(_directoryService.TempDirectory);

        // Read file into Markdown
        var htmlContent  = _markdown.Transform(await _directoryService.FileSystem.File.ReadAllTextAsync(tempDownloadFile));
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlContent);

        // Find the table of Native Themes
        var tableContent = htmlDoc.DocumentNode
            .SelectSingleNode("//h2[contains(text(),'Native Themes')]/following-sibling::p").InnerText;

        // Initialize dictionary to store theme metadata
        var themes = new Dictionary<string, ThemeMetadata>();


        // Split the table content by rows
        var rows = tableContent.Split("\r\n").Select(row => row.Trim()).Where(row => !string.IsNullOrWhiteSpace(row)).ToList();

        // Parse each row in the Native Themes table
        foreach (var row in rows.Skip(2))
        {

            var cells = row.Split('|').Skip(1).Select(cell => cell.Trim()).ToList();

            // Extract information from each cell
            var themeName = cells[0];
            var authorName = cells[1];
            var description = cells[2];
            var compatibility = Version.Parse(cells[3]);

            // Create ThemeMetadata object
            var themeMetadata = new ThemeMetadata
            {
                Author = authorName,
                Description = description,
                LastCompatible = compatibility
            };

            // Add theme metadata to dictionary
            themes.Add(themeName, themeMetadata);
        }

        return themes;
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
