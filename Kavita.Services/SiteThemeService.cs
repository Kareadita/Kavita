using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.Theme;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums.Theme;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;
using Markdig;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Markdown = Markdig.Markdown;

namespace Kavita.Services;

internal class GitHubContent
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonPropertyName("download_url")]
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

public class ThemeService(
    IDirectoryService directoryService,
    IUnitOfWork unitOfWork,
    IEventHub eventHub,
    ILogger<ThemeService> logger,
    IMemoryCache cache)
    : IThemeService
{
    private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseGithub().Build();

    private readonly MemoryCacheEntryOptions _cacheOptions = new MemoryCacheEntryOptions()
        .SetSize(1)
        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

    private const string GithubBaseUrl = "https://api.github.com";

    /// <summary>
    /// Used for refreshing metadata around themes
    /// </summary>
    private const string GithubReadme = "https://raw.githubusercontent.com/Kareadita/Themes/main/README.md";

    /// <summary>
    /// Given a themeId, return the content inside that file
    /// </summary>
    /// <param name="themeId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<string> GetContent(int themeId, CancellationToken ct = default)
    {
        var theme = await unitOfWork.SiteThemeRepository.GetThemeDto(themeId) ?? throw new KavitaException("theme-doesnt-exist");
        var themeFile = directoryService.FileSystem.Path.Join(directoryService.SiteThemeDirectory, theme.FileName);
        if (string.IsNullOrEmpty(themeFile) || !directoryService.FileSystem.File.Exists(themeFile))
            throw new KavitaException("theme-doesnt-exist");

        return await directoryService.FileSystem.File.ReadAllTextAsync(themeFile, ct);
    }

    public async Task<List<DownloadableSiteThemeDto>> GetDownloadableThemes(CancellationToken ct = default)
    {
        const string cacheKey = "browse";
        // Avoid a duplicate Dark issue some users faced during migration
        var existingThemes = (await unitOfWork.SiteThemeRepository.GetThemeDtos())
            .GroupBy(k => k.Name)
            .ToDictionary(g => g.Key, g => g.First());

        if (cache.TryGetValue(cacheKey, out List<DownloadableSiteThemeDto>? themes) && themes != null)
        {
            foreach (var t in themes)
            {
                t.AlreadyDownloaded = existingThemes.ContainsKey(t.Name);
            }
            return themes;
        }

        // Fetch contents of the Native Themes directory
        var themesContents = await GetDirectoryContent("Native%20Themes", ct);

        // Filter out directories
        var themeDirectories = themesContents.Where(c => c.Type == "dir").ToList();

        // Get the Readme and augment the theme data
        var themeMetadata = await GetReadme();

        var themeDtos = new List<DownloadableSiteThemeDto>();
        foreach (var themeDir in themeDirectories)
        {
            var themeName = themeDir.Name.Trim();

            // Fetch contents of the theme directory
            var themeContents = await GetDirectoryContent(themeDir.Path, ct);


            // Find css and preview files
            var cssFile = themeContents.FirstOrDefault(c => c.Name.EndsWith(".css"));
            var previewUrls = GetPreviewUrls(themeContents);

            if (cssFile == null) continue;

            var cssUrl = cssFile.DownloadUrl;


            var dto = new DownloadableSiteThemeDto()
            {
                Name = themeName,
                CssUrl = cssUrl,
                CssFile = cssFile.Name,
                PreviewUrls = previewUrls,
                Sha = cssFile.Sha,
                Path = themeDir.Path,
            };

            if (themeMetadata.TryGetValue(themeName, out var metadata))
            {
                dto.Author = metadata.Author;
                dto.LastCompatibleVersion = metadata.LastCompatible.ToString();
                dto.IsCompatible = BuildInfo.Version <= metadata.LastCompatible;
                dto.AlreadyDownloaded = existingThemes.ContainsKey(themeName);
                dto.Description = metadata.Description;
            }

            themeDtos.Add(dto);
        }

        cache.Set(cacheKey, themeDtos, _cacheOptions);

        return themeDtos;
    }

    private static List<string> GetPreviewUrls(IEnumerable<GitHubContent> themeContents)
    {
        return themeContents
            .Where(c => Parser.IsImage(c.Name) )
            .Select(p => p.DownloadUrl)
            .ToList();
    }

    private static async Task<IList<GitHubContent>> GetDirectoryContent(string path, CancellationToken ct = default)
    {
        var json = await $"{GithubBaseUrl}/repos/Kareadita/Themes/contents/{path}"
            .WithHeader(HeaderNames.Accept, "application/vnd.github+json")
            .WithHeader(HeaderNames.UserAgent, "Kavita")
            .GetStringAsync(cancellationToken: ct);

        return string.IsNullOrEmpty(json) ? [] : JsonConvert.DeserializeObject<List<GitHubContent>>(json) ?? [];
    }

    /// <summary>
    /// Returns a map of all Native Themes names mapped to their metadata
    /// </summary>
    /// <returns></returns>
    private async Task<IDictionary<string, ThemeMetadata>> GetReadme()
    {
        // Try and delete a Readme file if it already exists
        var existingReadmeFile = directoryService.FileSystem.Path.Join(directoryService.TempDirectory, "README.md");
        if (directoryService.FileSystem.File.Exists(existingReadmeFile))
        {
            directoryService.DeleteFiles([existingReadmeFile]);
        }

        var tempDownloadFile = await GithubReadme.DownloadFileAsync(directoryService.TempDirectory);

        // Read file into Markdown
        var htmlContent  = Markdown.ToHtml(await directoryService.FileSystem.File.ReadAllTextAsync(tempDownloadFile), _markdownPipeline);
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

        directoryService.ExistOrCreate(directoryService.SiteThemeDirectory);
        var existingTempFile = directoryService.FileSystem.Path.Join(directoryService.SiteThemeDirectory,
            directoryService.FileSystem.FileInfo.New(dto.CssUrl).Name);
        directoryService.DeleteFiles([existingTempFile]);

        var tempDownloadFile = await FlurlConfiguration.CreateSafeRequest(dto.CssUrl)
            .DownloadFileAsync(directoryService.TempDirectory);

        // Validate the hash on the downloaded file
        // if (!_fileService.ValidateSha(tempDownloadFile, dto.Sha))
        // {
        //     throw new KavitaException("Cannot download theme, hash does not match");
        // }

        directoryService.CopyFileToDirectory(tempDownloadFile, directoryService.SiteThemeDirectory);
        var finalLocation = directoryService.FileSystem.Path.Join(directoryService.SiteThemeDirectory, dto.CssFile);

        return finalLocation;
    }


    public async Task<SiteTheme> DownloadRepoTheme(DownloadableSiteThemeDto dto, CancellationToken ct = default)
    {

        // Validate we don't have a collision with existing or existing doesn't already exist
        var existingThemes = directoryService.ScanFiles(directoryService.SiteThemeDirectory, string.Empty);
        if (existingThemes.Any(f => Path.GetFileName(f) == dto.CssFile))
        {
            // This can happen if you delete then immediately download (to refresh). We should just delete the old file and download. Users can always rollback their version with github directly
            directoryService.DeleteFiles(existingThemes.Where(f => Path.GetFileName(f) == dto.CssFile));
        }

        var finalLocation = await DownloadSiteTheme(dto);

        // Create a new entry and note that this is downloaded
        var theme = new SiteTheme()
        {
            Name = dto.Name,
            NormalizedName = dto.Name.ToNormalized(),
            FileName = directoryService.FileSystem.Path.GetFileName(finalLocation),
            Provider = ThemeProvider.Custom,
            IsDefault = false,
            GitHubPath = dto.Path,
            Description = dto.Description,
            PreviewUrls = string.Join('|', dto.PreviewUrls),
            Author = dto.Author,
            ShaHash = dto.Sha,
            CompatibleVersion = dto.LastCompatibleVersion,
        };
        unitOfWork.SiteThemeRepository.Add(theme);

        await unitOfWork.CommitAsync(ct);

        // Inform about the new theme
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.SiteThemeProgressEvent(directoryService.FileSystem.Path.GetFileName(theme.FileName), theme.Name,
                ProgressEventType.Ended), ct: ct);
        return theme;
    }

    public async Task SyncThemes(CancellationToken ct = default)
    {
        var themes = await unitOfWork.SiteThemeRepository.GetThemes();
        var themeMetadata = await GetReadme();
        foreach (var theme in themes)
        {
            await SyncTheme(theme, themeMetadata);
        }
        logger.LogInformation("Sync Themes complete");
    }

    /// <summary>
    /// If the Theme is from the Theme repo, see if there is a new version that is compatible
    /// </summary>
    /// <param name="theme"></param>
    /// <param name="themeMetadata">The Readme information</param>
    private async Task SyncTheme(SiteTheme? theme, IDictionary<string, ThemeMetadata> themeMetadata)
    {
        // Given a theme, first validate that it is applicable
        if (theme == null || theme.Provider == ThemeProvider.System || string.IsNullOrEmpty(theme.GitHubPath))
        {
            logger.LogInformation("Cannot Sync {ThemeName} as it is not valid", theme?.Name);
            return;
        }

        if (new Version(theme.CompatibleVersion) > BuildInfo.Version)
        {
            logger.LogDebug("{ThemeName} theme supports a more up-to-date version ({Version}) of Kavita. Please update", theme.Name, theme.CompatibleVersion);
            return;
        }


        var themeContents = await GetDirectoryContent(theme.GitHubPath);
        var cssFile = themeContents.FirstOrDefault(c => c.Name.EndsWith(".css"));

        if (cssFile == null) return;

        // Update any metadata
        if (themeMetadata.TryGetValue(theme.Name, out var metadata))
        {
            theme.Description = metadata.Description;
            theme.Author = metadata.Author;
            theme.CompatibleVersion = metadata.LastCompatible.ToString();
            theme.PreviewUrls = string.Join('|', GetPreviewUrls(themeContents));
        }

        var hasUpdated = cssFile.Sha != theme.ShaHash;
        if (hasUpdated)
        {
            logger.LogDebug("Theme {ThemeName} is out of date, updating", theme.Name);
            var tempLocation = directoryService.FileSystem.Path.Join(directoryService.TempDirectory, theme.FileName);

            directoryService.DeleteFiles([tempLocation]);

            var location = await FlurlConfiguration.CreateSafeRequest(cssFile.DownloadUrl)
                .DownloadFileAsync(directoryService.TempDirectory);
            if (directoryService.FileSystem.File.Exists(location))
            {
                directoryService.CopyFileToDirectory(location, directoryService.SiteThemeDirectory);
                logger.LogInformation("Updated Theme on disk for {ThemeName}", theme.Name);
            }
        }

        await unitOfWork.CommitAsync();


        if (hasUpdated)
        {
            await eventHub.SendMessageAsync(MessageFactory.SiteThemeUpdated,
                MessageFactory.SiteThemeUpdatedEvent(theme.Name));
        }

        // Send an update to refresh metadata around the themes
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.SiteThemeProgressEvent(directoryService.FileSystem.Path.GetFileName(theme.FileName), theme.Name,
                ProgressEventType.Ended));

        logger.LogInformation("Theme Sync complete");
    }

    /// <summary>
    /// Deletes a SiteTheme. The CSS file will be moved to temp/ to allow user to recover data
    /// </summary>
    /// <param name="siteThemeId"></param>
    /// <param name="ct"></param>
    public async Task DeleteTheme(int siteThemeId, CancellationToken ct = default)
    {
        // Validate no one else is using this theme
        var inUse = await unitOfWork.SiteThemeRepository.IsThemeInUse(siteThemeId);
        if (inUse)
        {
            throw new KavitaException("errors.delete-theme-in-use");
        }

        var siteTheme = await unitOfWork.SiteThemeRepository.GetTheme(siteThemeId);
        if (siteTheme == null) return;

        await RemoveTheme(siteTheme);
    }

    /// <summary>
    /// This assumes a file is already in temp directory and will be used for
    /// </summary>
    /// <param name="tempFile"></param>
    /// <param name="username"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<SiteTheme> CreateThemeFromFile(string tempFile, string username, CancellationToken ct = default)
    {
        if (!directoryService.FileSystem.File.Exists(tempFile))
        {
            logger.LogInformation("Unable to create theme from manual upload as file not in temp");
            throw new KavitaException("errors.theme-manual-upload");
        }


        var filename = directoryService.FileSystem.FileInfo.New(tempFile).Name;
        var themeName = Path.GetFileNameWithoutExtension(filename);

        if (await unitOfWork.SiteThemeRepository.GetThemeDtoByName(themeName) != null)
        {
            throw new KavitaException("errors.theme-already-in-use");
        }

        directoryService.CopyFileToDirectory(tempFile, directoryService.SiteThemeDirectory);
        var finalLocation = directoryService.FileSystem.Path.Join(directoryService.SiteThemeDirectory, filename);


        // Create a new entry and note that this is downloaded
        var theme = new SiteTheme()
        {
            Name = Path.GetFileNameWithoutExtension(filename),
            NormalizedName = themeName.ToNormalized(),
            FileName = directoryService.FileSystem.Path.GetFileName(finalLocation),
            Provider = ThemeProvider.Custom,
            IsDefault = false,
            Description = $"Manually uploaded via UI by {username}",
            PreviewUrls = string.Empty,
            Author = username,
        };
        unitOfWork.SiteThemeRepository.Add(theme);

        await unitOfWork.CommitAsync(ct);

        // Inform about the new theme
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.SiteThemeProgressEvent(directoryService.FileSystem.Path.GetFileName(theme.FileName), theme.Name,
                ProgressEventType.Ended), ct: ct);
        return theme;

    }


    /// <summary>
    /// Removes the theme and any references to it from Pref and sets them to the default at the time.
    /// This commits to DB.
    /// </summary>
    /// <param name="theme"></param>
    private async Task RemoveTheme(SiteTheme theme)
    {
        logger.LogInformation("Removing {ThemeName}. File can be found in temp/ until nightly cleanup", theme.Name);
        var prefs = await unitOfWork.UserRepository.GetAllPreferencesByThemeAsync(theme.Id);
        var defaultTheme = await unitOfWork.SiteThemeRepository.GetDefaultTheme();
        foreach (var pref in prefs)
        {
            pref.Theme = defaultTheme;
            unitOfWork.UserRepository.Update(pref);
        }

        try
        {
            // Copy the theme file to temp for nightly removal (to give user time to reclaim if made a mistake)
            var existingLocation =
                directoryService.FileSystem.Path.Join(directoryService.SiteThemeDirectory, theme.FileName);
            var newLocation =
                directoryService.FileSystem.Path.Join(directoryService.TempDirectory, theme.FileName);

            if (!directoryService.FileSystem.File.Exists(newLocation))
            {
                logger.LogInformation("Copying Deleted theme file ({FileName}) to config/temp, it will be removed at midnight", theme.FileName);
                directoryService.CopyFileToDirectory(existingLocation, newLocation);
            }

            directoryService.DeleteFiles([existingLocation]);
        }
        catch (Exception) { /* Swallow */ }


        unitOfWork.SiteThemeRepository.Remove(theme);
        await unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Updates the themeId to the default theme, all others are marked as non-default
    /// </summary>
    /// <param name="themeId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException">If theme does not exist</exception>
    public async Task UpdateDefault(int themeId, CancellationToken ct = default)
    {
        try
        {
            var theme = await unitOfWork.SiteThemeRepository.GetThemeDto(themeId);
            if (theme == null) throw new KavitaException("theme-doesnt-exist");

            foreach (var siteTheme in await unitOfWork.SiteThemeRepository.GetThemes())
            {
                siteTheme.IsDefault = (siteTheme.Id == themeId);
                unitOfWork.SiteThemeRepository.Update(siteTheme);
            }

            if (!unitOfWork.HasChanges()) return;
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception)
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}
