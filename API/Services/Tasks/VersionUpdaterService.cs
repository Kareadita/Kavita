using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.DTOs.Update;
using API.Extensions;
using API.SignalR;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using MarkdownDeep;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;
#nullable enable

internal class GithubReleaseMetadata
{
    /// <summary>
    /// Name of the Tag
    /// <example>v0.4.3</example>
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public required string Tag_Name { get; init; }
    /// <summary>
    /// Name of the Release
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Body of the Release
    /// </summary>
    public required string Body { get; init; }
    /// <summary>
    /// Url of the release on GitHub
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public required string Html_Url { get; init; }
    /// <summary>
    /// Date Release was Published
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public required string Published_At { get; init; }
}

public interface IVersionUpdaterService
{
    Task<UpdateNotificationDto?> CheckForUpdate();
    Task PushUpdate(UpdateNotificationDto update);
    Task<IList<UpdateNotificationDto>> GetAllReleases(int count = 0);
    Task<int> GetNumberOfReleasesBehind(bool stableOnly = false);
    void BustGithubCache();
}


public partial class VersionUpdaterService : IVersionUpdaterService
{
    private readonly ILogger<VersionUpdaterService> _logger;
    private readonly IEventHub _eventHub;
    private readonly Markdown _markdown = new MarkdownDeep.Markdown();
#pragma warning disable S1075
    private const string GithubLatestReleasesUrl = "https://api.github.com/repos/Kareadita/Kavita/releases/latest";
    private const string GithubAllReleasesUrl = "https://api.github.com/repos/Kareadita/Kavita/releases";
    private const string GithubPullsUrl = "https://api.github.com/repos/Kareadita/Kavita/pulls/";
    private const string GithubBranchCommitsUrl = "https://api.github.com/repos/Kareadita/Kavita/commits?sha=develop";
#pragma warning restore S1075

    [GeneratedRegex(@"^\n*(.*?)\n+#{1,2}\s", RegexOptions.Singleline)]
    private static partial Regex BlogPartRegex();
    private readonly string _cacheFilePath;
    /// <summary>
    /// The latest release cache
    /// </summary>
    private readonly string _cacheLatestReleaseFilePath;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public VersionUpdaterService(ILogger<VersionUpdaterService> logger, IEventHub eventHub, IDirectoryService directoryService)
    {
        _logger = logger;
        _eventHub = eventHub;
        _cacheFilePath = Path.Combine(directoryService.LongTermCacheDirectory, "github_releases_cache.json");
        _cacheLatestReleaseFilePath = Path.Combine(directoryService.LongTermCacheDirectory, "github_latest_release_cache.json");

        FlurlConfiguration.ConfigureClientForUrl(GithubLatestReleasesUrl);
        FlurlConfiguration.ConfigureClientForUrl(GithubAllReleasesUrl);
    }

    /// <summary>
    /// Fetches the latest (stable) release from GitHub. Does not do any extra nightly release parsing.
    /// </summary>
    /// <returns>Latest update</returns>
    public async Task<UpdateNotificationDto?> CheckForUpdate()
    {
        // Attempt to fetch from cache
        var cachedRelease = await TryGetCachedLatestRelease();
        if (cachedRelease != null)
        {
            return cachedRelease;
        }

        var update = await GetGithubRelease();
        var dto = CreateDto(update);

        if (dto != null)
        {
            await CacheLatestReleaseAsync(dto);
        }

        return dto;
    }

    /// <summary>
    /// Will add any extra (nightly) updates from the latest stable. Does not back-fill anything prior to the latest stable.
    /// </summary>
    /// <param name="dtos"></param>
    private async Task EnrichWithNightlyInfo(List<UpdateNotificationDto> dtos)
    {
        var dto = dtos[0]; // Latest version
        try
        {
            var currentVersion = new Version(dto.CurrentVersion);
            var nightlyReleases = await GetNightlyReleases(currentVersion, Version.Parse(dto.UpdateVersion));

            if (nightlyReleases.Count == 0) return;

            // Create new DTOs for each nightly release and insert them at the beginning of the list
            var nightlyDtos = new List<UpdateNotificationDto>();
            foreach (var nightly in nightlyReleases)
            {
                var prInfo = await FetchPullRequestInfo(nightly.PrNumber);
                if (prInfo == null) continue;

                var sections = ParseReleaseBody(prInfo.Body);
                var blogPart = ExtractBlogPart(prInfo.Body);

                var nightlyDto = new UpdateNotificationDto
                {
                    // TODO: I should pass Title to the FE so that Nightly Release can be localized
                    UpdateTitle = $"Nightly Release {nightly.Version} - {prInfo.Title}",
                    UpdateVersion = nightly.Version,
                    CurrentVersion = dto.CurrentVersion,
                    UpdateUrl = prInfo.Html_Url,
                    PublishDate = prInfo.Merged_At,
                    IsDocker = true, // Nightlies are always Docker Only
                    IsReleaseEqual = IsVersionEqualToBuildVersion(Version.Parse(nightly.Version)),
                    IsReleaseNewer = true, // Since we already filtered these in GetNightlyReleases
                    IsPrerelease = true, // All Nightlies are considered prerelease
                    Added = sections.TryGetValue("Added", out var added) ? added : [],
                    Changed = sections.TryGetValue("Changed", out var changed) ? changed : [],
                    Fixed = sections.TryGetValue("Fixed", out var bugfixes) ? bugfixes : [],
                    Removed = sections.TryGetValue("Removed", out var removed) ? removed : [],
                    Theme = sections.TryGetValue("Theme", out var theme) ? theme : [],
                    Developer = sections.TryGetValue("Developer", out var developer) ? developer : [],
                    KnownIssues = sections.TryGetValue("KnownIssues", out var knownIssues) ? knownIssues : [],
                    Api = sections.TryGetValue("Api", out var api) ? api : [],
                    FeatureRequests = sections.TryGetValue("Feature Requests", out var frs) ? frs : [],
                    BlogPart = _markdown.Transform(blogPart.Trim()),
                    UpdateBody = _markdown.Transform(prInfo.Body.Trim())
                };

                nightlyDtos.Add(nightlyDto);
            }

            // Insert nightly releases at the beginning of the list
            var sortedNightlyDtos = nightlyDtos.OrderByDescending(x => x.PublishDate).ToList();
            dtos.InsertRange(0, sortedNightlyDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enrich nightly release information");
        }
    }


    private async Task<PullRequestInfo?> FetchPullRequestInfo(int prNumber)
    {
        try
        {
            return await $"{GithubPullsUrl}{prNumber}"
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .GetJsonAsync<PullRequestInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch PR information for #{PrNumber}", prNumber);
            return null;
        }
    }

    private async Task<List<NightlyInfo>> GetNightlyReleases(Version currentVersion, Version latestStableVersion)
    {
        try
        {
            var nightlyReleases = new List<NightlyInfo>();

            var commits = await GithubBranchCommitsUrl
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .GetJsonAsync<IList<CommitInfo>>();

            var commitList = commits.ToList();
            bool foundLastStable = false;

            for (var i = 0; i < commitList.Count - 1; i++)
            {
                var commit = commitList[i];
                var message = commit.Commit.Message.Split('\n')[0]; // Take first line only

                // Skip [skip ci] commits
                if (message.Contains("[skip ci]")) continue;

                // Check if this is a stable release
                if (message.StartsWith('v'))
                {
                    var stableMatch = Regex.Match(message, @"v(\d+\.\d+\.\d+\.\d+)");
                    if (stableMatch.Success)
                    {
                        var stableVersion = new Version(stableMatch.Groups[1].Value);
                        // If we find a stable version lower than current, we've gone too far back
                        if (stableVersion <= currentVersion)
                        {
                            foundLastStable = true;
                            break;
                        }
                    }
                    continue;
                }

                // Look for version bumps that follow PRs
                if (!foundLastStable && message == "Bump versions by dotnet-bump-version.")
                {
                    // Get the PR commit that triggered this version bump
                    if (i + 1 < commitList.Count)
                    {
                        var prCommit = commitList[i + 1];
                        var prMessage = prCommit.Commit.Message.Split('\n')[0];

                        // Extract PR number using improved regex
                        var prMatch = Regex.Match(prMessage, @"(?:^|\s)\(#(\d+)\)|\s#(\d+)");
                        if (!prMatch.Success) continue;

                        var prNumber = int.Parse(prMatch.Groups[1].Value != "" ?
                            prMatch.Groups[1].Value : prMatch.Groups[2].Value);

                        // Get the version from AssemblyInfo.cs in this commit
                        var version = await GetVersionFromCommit(commit.Sha);
                        if (version == null) continue;

                        // Parse version and compare with current version
                        if (Version.TryParse(version, out var parsedVersion) &&
                            parsedVersion > latestStableVersion)
                        {
                            nightlyReleases.Add(new NightlyInfo
                            {
                                Version = version,
                                PrNumber = prNumber,
                                Date = DateTime.Parse(commit.Commit.Author.Date, CultureInfo.InvariantCulture)
                            });
                        }
                    }
                }
            }

            return nightlyReleases.OrderByDescending(x => x.Date).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get nightly releases");
            return [];
        }
    }

    public async Task<IList<UpdateNotificationDto>> GetAllReleases(int count = 0)
    {
        // Attempt to fetch from cache
        var cachedReleases = await TryGetCachedReleases();
        // If there is a cached release and the current version is within it, use it, otherwise regenerate
        if (cachedReleases != null && cachedReleases.Any(r => IsVersionEqual(r.UpdateVersion, BuildInfo.Version.ToString())))
        {
            if (count > 0)
            {
                // NOTE: We may want to allow the admin to clear Github cache
                return cachedReleases.Take(count).ToList();
            }

            return cachedReleases;
        }

        var updates = await GetGithubReleases();
        var query = updates.Select(CreateDto)
            .Where(d => d != null)
            .OrderByDescending(d => d!.PublishDate)
            .Select(d => d!);

        var updateDtos = query.ToList();

        // Sometimes a release can be 0.8.5.0 on disk, but 0.8.5 from Github
        var versionParts = updateDtos[0].UpdateVersion.Split('.');
        if (versionParts.Length < 4)
        {
            updateDtos[0].UpdateVersion += ".0"; // Append missing parts
        }

        // If we're on a nightly build, enrich the information
        if (updateDtos.Count != 0) // && BuildInfo.Version > new Version(updateDtos[0].UpdateVersion)
        {
            await EnrichWithNightlyInfo(updateDtos);
        }

        // Find the latest dto
        var latestRelease = updateDtos[0]!;
        var updateVersion = new Version(latestRelease.UpdateVersion);
        var isNightly = BuildInfo.Version > new Version(latestRelease.UpdateVersion);

        // isNightly can be true when we compare something like v0.8.1 vs v0.8.1.0
        if (IsVersionEqualToBuildVersion(updateVersion))
        {
            isNightly = false;
        }


        latestRelease.IsOnNightlyInRelease = isNightly;

        // Cache the fetched data
        if (updateDtos.Count > 0)
        {
            await CacheReleasesAsync(updateDtos);
        }

        if (count > 0)
        {
            return updateDtos.Take(count).ToList();
        }

        return updateDtos;
    }

    /// <summary>
    /// Compares 2 versions and ensures that the minor is always there
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <returns></returns>
    private static bool IsVersionEqual(string v1, string v2)
    {
        var versionParts = v1.Split('.');
        if (versionParts.Length < 4)
        {
            v1 += ".0"; // Append missing parts
        }

        versionParts = v2.Split('.');
        if (versionParts.Length < 4)
        {
            v2 += ".0"; // Append missing parts
        }

        return string.Equals(v2, v2, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IList<UpdateNotificationDto>?> TryGetCachedReleases()
    {
        if (!File.Exists(_cacheFilePath)) return null;

        var fileInfo = new FileInfo(_cacheFilePath);
        if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc <= CacheDuration)
        {
            var cachedData = await File.ReadAllTextAsync(_cacheFilePath);
            return JsonSerializer.Deserialize<IList<UpdateNotificationDto>>(cachedData);
        }

        return null;
    }

    private async Task<UpdateNotificationDto?> TryGetCachedLatestRelease()
    {
        if (!File.Exists(_cacheLatestReleaseFilePath)) return null;

        var fileInfo = new FileInfo(_cacheLatestReleaseFilePath);
        if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc <= CacheDuration)
        {
            var cachedData = await File.ReadAllTextAsync(_cacheLatestReleaseFilePath);
            return JsonSerializer.Deserialize<UpdateNotificationDto>(cachedData);
        }

        return null;
    }

    private async Task CacheReleasesAsync(IList<UpdateNotificationDto> updates)
    {
        try
        {
            var json = JsonSerializer.Serialize(updates, JsonOptions);
            await File.WriteAllTextAsync(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache releases");
        }
    }

    private async Task CacheLatestReleaseAsync(UpdateNotificationDto update)
    {
        try
        {
            var json = JsonSerializer.Serialize(update, JsonOptions);
            await File.WriteAllTextAsync(_cacheLatestReleaseFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache latest release");
        }
    }



    private static bool IsVersionEqualToBuildVersion(Version updateVersion)
    {
        return updateVersion == BuildInfo.Version || (updateVersion.Revision < 0 && BuildInfo.Version.Revision == 0 &&
                                                      BuildInfo.Version.CompareWithoutRevision(updateVersion));
    }


    /// <summary>
    /// Returns the number of releases ahead of this install version. If this install version is on a nightly,
    /// then include nightly releases, otherwise only count Stable releases.
    /// </summary>
    /// <param name="stableOnly">Only count Stable releases </param>
    /// <returns></returns>
    public async Task<int> GetNumberOfReleasesBehind(bool stableOnly = false)
    {
        var updates = await GetAllReleases();

        // If the user is on nightly, then we need to handle releases behind differently
        if (!stableOnly && (updates[0].IsPrerelease || updates[0].IsOnNightlyInRelease))
        {
            return updates.Count(u => u.IsReleaseNewer);
        }

        return updates
            .Where(update => !update.IsPrerelease)
            .Count(u => u.IsReleaseNewer);
    }

    /// <summary>
    /// Clears the Github cache
    /// </summary>
    public void BustGithubCache()
    {
        try
        {
            File.Delete(_cacheFilePath);
            File.Delete(_cacheLatestReleaseFilePath);
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Github cache");
        }
    }

    private UpdateNotificationDto? CreateDto(GithubReleaseMetadata? update)
    {
        if (update == null || string.IsNullOrEmpty(update.Tag_Name)) return null;
        var updateVersion = new Version(update.Tag_Name.Replace("v", string.Empty));
        var currentVersion = BuildInfo.Version.ToString(4);

        var bodyHtml = _markdown.Transform(update.Body.Trim());
        var parsedSections = ParseReleaseBody(update.Body);
        var blogPart = _markdown.Transform(ExtractBlogPart(update.Body).Trim());

        return new UpdateNotificationDto()
        {
            CurrentVersion = currentVersion,
            UpdateVersion = updateVersion.ToString(),
            UpdateBody = bodyHtml,
            UpdateTitle = update.Name,
            UpdateUrl = update.Html_Url,
            IsDocker = OsInfo.IsDocker,
            PublishDate = update.Published_At,
            IsReleaseEqual = IsVersionEqualToBuildVersion(updateVersion),
            IsReleaseNewer = BuildInfo.Version < updateVersion,
            IsPrerelease = false,

            Added = parsedSections.TryGetValue("Added", out var added) ? added : [],
            Removed = parsedSections.TryGetValue("Removed", out var removed) ? removed : [],
            Changed = parsedSections.TryGetValue("Changed", out var changed) ? changed : [],
            Fixed = parsedSections.TryGetValue("Fixed", out var fixes) ? fixes : [],
            Theme = parsedSections.TryGetValue("Theme", out var theme) ? theme : [],
            Developer = parsedSections.TryGetValue("Developer", out var developer) ? developer : [],
            KnownIssues = parsedSections.TryGetValue("Known Issues", out var knownIssues) ? knownIssues : [],
            Api = parsedSections.TryGetValue("Api", out var api) ? api : [],
            FeatureRequests = parsedSections.TryGetValue("Feature Requests", out var frs) ? frs : [],
            BlogPart = blogPart
        };
    }


    public async Task PushUpdate(UpdateNotificationDto? update)
    {
        if (update == null) return;

        var updateVersion = new Version(update.UpdateVersion);

        if (BuildInfo.Version < updateVersion)
        {
            _logger.LogWarning("Server is out of date. Current: {CurrentVersion}. Available: {AvailableUpdate}", BuildInfo.Version, updateVersion);
            await _eventHub.SendMessageAsync(MessageFactory.UpdateAvailable, MessageFactory.UpdateVersionEvent(update),
                true);
        }
    }

    private async Task<string?> GetVersionFromCommit(string commitSha)
    {
        try
        {
            // Use the raw GitHub URL format for the csproj file
            var content = await $"https://raw.githubusercontent.com/Kareadita/Kavita/{commitSha}/Kavita.Common/Kavita.Common.csproj"
                .WithHeader("User-Agent", "Kavita")
                .GetStringAsync();

            var versionMatch = Regex.Match(content, @"<AssemblyVersion>([0-9\.]+)</AssemblyVersion>");
            return versionMatch.Success ? versionMatch.Groups[1].Value : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get version from commit {Sha}: {Message}", commitSha, ex.Message);
            return null;
        }
    }



    private static async Task<GithubReleaseMetadata> GetGithubRelease()
    {
        var update = await GithubLatestReleasesUrl
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .GetJsonAsync<GithubReleaseMetadata>();

        return update;
    }

    private static async Task<IList<GithubReleaseMetadata>> GetGithubReleases()
    {
        var update = await GithubAllReleasesUrl
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .GetJsonAsync<IList<GithubReleaseMetadata>>();

        return update;
    }

    private static string ExtractBlogPart(string body)
    {
        if (body.StartsWith('#')) return string.Empty;
        var match = BlogPartRegex().Match(body);
        return match.Success ? match.Groups[1].Value.Trim() : body.Trim();
    }

    private static Dictionary<string, List<string>> ParseReleaseBody(string body)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var lines = body.Split('\n');
        string? currentSection = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Check for section headers (case-insensitive)
            if (trimmedLine.StartsWith('#'))
            {
                currentSection = trimmedLine.TrimStart('#').Trim();
                sections[currentSection] = [];
                continue;
            }

            // Parse items under a section
            if (currentSection != null &&
                trimmedLine.StartsWith("- ") &&
                !string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Remove "Fixed:", "Added:" etc. if present
                var cleanedItem = CleanSectionItem(trimmedLine);

                // Some sections like API/Developer/Removed don't have the title repeated, so we need to check for an additional cleaning
                if (cleanedItem.StartsWith("- "))
                {
                    cleanedItem =  trimmedLine.Substring(2);
                }

                // Only add non-empty items
                if (!string.IsNullOrWhiteSpace(cleanedItem))
                {
                    sections[currentSection].Add(cleanedItem);
                }
            }
        }

        return sections;
    }

    private static string CleanSectionItem(string item)
    {
        // Remove everything up to and including the first ":"
        var colonIndex = item.IndexOf(':');
        if (colonIndex != -1)
        {
            item = item.Substring(colonIndex + 1).Trim();
        }

        return item;
    }

    private sealed class PullRequestInfo
    {
        public required string Title { get; init; }
        public required string Body { get; init; }
        public required string Html_Url { get; init; }
        public required string Merged_At { get; init; }
        public required int Number { get; init; }
    }

    private sealed class CommitInfo
    {
        public required string Sha { get; init; }
        public required CommitDetail Commit { get; init; }
        public required string Html_Url { get; init; }
    }

    private sealed class CommitDetail
    {
        public required string Message { get; init; }
        public required CommitAuthor Author { get; init; }
    }

    private sealed class CommitAuthor
    {
        public required string Date { get; init; }
    }

    private sealed class NightlyInfo
    {
        public required string Version { get; init; }
        public required int PrNumber { get; init; }
        public required DateTime Date { get; init; }
    }
}
