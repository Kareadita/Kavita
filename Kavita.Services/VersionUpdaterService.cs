using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.Update;
using Kavita.Services.Extensions;
using Markdig;
using Microsoft.Extensions.Logging;
using Markdown = Markdig.Markdown;

namespace Kavita.Services;

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


public partial class VersionUpdaterService : IVersionUpdaterService
{
    private readonly ILogger<VersionUpdaterService> _logger;
    private readonly IEventHub _eventHub;
    private readonly IGithubApiService _githubApiService;
    private readonly IFileCacheService _fileCacheService;
    private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseGithub().Build();

    private const string KavitaRepo = "Kareadita/Kavita";
    private const string ReleasesPath = $"repos/{KavitaRepo}/releases";
    private const string PullsPath = $"repos/{KavitaRepo}/pulls/";
    private const string DevelopCommitsPath = $"repos/{KavitaRepo}/commits?sha=develop";
    private const string ReleasesCacheKey = "releases";

    [GeneratedRegex(@"^\n*([\s\S]*?)\n+^#\s(Added|Fixed|Changed|Theme|API|Removed)", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex BlogPartRegex();
    [GeneratedRegex(@"Release changes for (\d+\.\d+\.\d+\.\d+)", RegexOptions.Multiline | RegexOptions.Compiled, 1_000)]
    private static partial Regex NightlyReleaseRegex();

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan PrCacheDuration = TimeSpan.FromDays(7);

    public VersionUpdaterService(ILogger<VersionUpdaterService> logger, IEventHub eventHub,
        IGithubApiService githubApiService, IFileCacheService fileCacheService)
    {
        _logger = logger;
        _eventHub = eventHub;
        _githubApiService = githubApiService;
        _fileCacheService = fileCacheService;
    }

    /// <summary>
    /// Returns the latest stable release, or null when GitHub cannot be reached and nothing is cached
    /// </summary>
    /// <param name="forceRefresh">Ask GitHub even when the cache is fresh, for a release published within the hour</param>
    /// <param name="ct"></param>
    /// <returns>Latest update</returns>
    public async Task<UpdateNotificationDto?> CheckForUpdate(bool forceRefresh = false, CancellationToken ct = default)
    {
        var releases = await GetReleases(forceRefresh, ct);
        return releases.FirstOrDefault(r => !r.IsPrerelease);
    }

    /// <summary>
    /// Will add any extra (nightly) updates from the latest stable. Does not back-fill anything prior to the latest stable.
    /// </summary>
    /// <param name="dtos"></param>
    /// <param name="forceRefresh"></param>
    /// <param name="ct"></param>
    private async Task EnrichWithNightlyInfo(List<UpdateNotificationDto> dtos, bool forceRefresh, CancellationToken ct)
    {
        var dto = dtos[0]; // Latest version
        try
        {
            var currentVersion = new Version(dto.CurrentVersion);
            var nightlyReleases = await GetNightlyReleases(currentVersion, Version.Parse(dto.UpdateVersion), forceRefresh, ct);

            if (nightlyReleases.Count == 0) return;

            // Create new DTOs for each nightly release and insert them at the beginning of the list
            var nightlyDtos = new List<UpdateNotificationDto>();
            foreach (var nightly in nightlyReleases)
            {
                var prInfo = (await _githubApiService.GetAsync<PullRequestInfo>($"{PullsPath}{nightly.PrNumber}", PrCacheDuration, ct: ct)).Value;
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
                    KnownIssues = sections.TryGetValue("Known Issues", out var knownIssues) ? knownIssues : [],
                    Api = sections.TryGetValue("Api", out var api) ? api : [],
                    FeatureRequests = sections.TryGetValue("Feature Requests", out var frs) ? frs : [],
                    BlogPart = Markdown.ToHtml(blogPart.Trim(), _markdownPipeline),
                    UpdateBody = Markdown.ToHtml(prInfo.Body.Trim(), _markdownPipeline)
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


    private async Task<List<NightlyInfo>> GetNightlyReleases(Version currentVersion, Version latestStableVersion,
        bool forceRefresh, CancellationToken ct)
    {
        try
        {
            var nightlyReleases = new List<NightlyInfo>();

            var commits = await _githubApiService.GetAsync<List<CommitInfo>>(DevelopCommitsPath, CacheDuration, forceRefresh, ct);
            if (!commits.HasValue) return [];

            var commitList = commits.Value;
            var foundLastStable = false;

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
                        var prNumber = GetPrNumber(prCommit);
                        if (prNumber is null) continue;

                        // Get the version from AssemblyInfo.cs in this commit
                        var version = await GetVersionFromCommit(commit.Sha, ct);
                        if (version == null) continue;

                        // Parse version and compare with current version
                        if (Version.TryParse(version, out var parsedVersion) &&
                            parsedVersion > latestStableVersion)
                        {
                            nightlyReleases.Add(new NightlyInfo
                            {
                                Version = version,
                                PrNumber = prNumber.Value,
                                Date = DateTime.Parse(commit.Commit.Author.Date, CultureInfo.InvariantCulture)
                            });
                        }
                    }
                }

                var newNightlyMatch = NightlyReleaseRegex().Match(message);
                if (!foundLastStable && newNightlyMatch.Success && i + 1 < commitList.Count)
                {
                    var prCommit = commitList[i + 1];
                    var prNumber = GetPrNumber(prCommit);
                    if (prNumber is null) continue;

                    var version = newNightlyMatch.Groups[1].Value;
                    if (Version.TryParse(version, out var parsedVersion) && parsedVersion > latestStableVersion)
                    {
                        nightlyReleases.Add(new NightlyInfo
                        {
                            Version = version,
                            PrNumber = prNumber.Value,
                            Date = DateTime.Parse(commit.Commit.Author.Date, CultureInfo.InvariantCulture)
                        });
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

    private static int? GetPrNumber(CommitInfo commitInfo)
    {
        var prMessage = commitInfo.Commit.Message.Split('\n')[0];

        // Extract PR number using improved regex
        var prMatch = Regex.Match(prMessage, @"(?:^|\s)\(#(\d+)\)|\s#(\d+)");
        if (!prMatch.Success)
        {
            return null;
        }

        var prNumber = prMatch.Groups[1].Value != "" ? prMatch.Groups[1].Value : prMatch.Groups[2].Value;

        return int.TryParse(prNumber, out var result) ? result : null;
    }

    /// <summary>
    /// Returns releases newest first. When GitHub cannot be reached, serves the last cached copy at any age, or an empty list.
    /// </summary>
    public async Task<IList<UpdateNotificationDto>> GetAllReleases(int count = 0, CancellationToken ct = default)
    {
        var releases = await GetReleases(forceRefresh: false, ct);
        return count > 0 ? releases.Take(count).ToList() : releases;
    }

    /// <summary>
    /// The built release list is cached separately from the raw GitHub responses: it depends on this install's version,
    /// and building it renders markdown and downloads csproj files for nightlies.
    /// </summary>
    private async Task<List<UpdateNotificationDto>> GetReleases(bool forceRefresh, CancellationToken ct)
    {
        var result = await _fileCacheService.GetOrFetchResultAsync(ReleasesCacheKey, FileCacheService.VersionDirectory,
            CacheDuration,
            token => BuildReleases(forceRefresh, token),
            shouldCache: releases => releases is { Count: > 0 },
            serveStaleOnFailure: true,
            forceRefresh: forceRefresh,
            ct: ct);

        return result.Value ?? [];
    }

    private async Task<List<UpdateNotificationDto>?> BuildReleases(bool forceRefresh, CancellationToken ct)
    {
        var releases = await _githubApiService.GetAsync<List<GithubReleaseMetadata>>(ReleasesPath, CacheDuration, forceRefresh, ct);

        var updateDtos = (releases.Value ?? [])
            .Select(CreateDto)
            .Where(d => d != null)
            .OrderByDescending(d => d!.PublishDate)
            .Select(d => d!)
            .ToList();

        if (updateDtos.Count == 0) return null;

        // Sometimes a release can be 0.8.5.0 on disk, but 0.8.5 from Github
        var versionParts = updateDtos[0].UpdateVersion.Split('.');
        if (versionParts.Length < 4)
        {
            updateDtos[0].UpdateVersion += ".0"; // Append missing parts
        }

        if (IsBuildNewerThan(new Version(updateDtos[0].UpdateVersion)))
        {
            await EnrichWithNightlyInfo(updateDtos, forceRefresh, ct);
        }

        var latestRelease = updateDtos[0];
        latestRelease.IsOnNightlyInRelease = IsBuildNewerThan(new Version(latestRelease.UpdateVersion));

        return updateDtos;
    }

    /// <summary>
    /// True when this install is ahead of the given release, i.e. on a nightly. v0.8.1 and v0.8.1.0 count as equal.
    /// </summary>
    private static bool IsBuildNewerThan(Version releaseVersion)
    {
        return BuildInfo.Version > releaseVersion && !IsVersionEqualToBuildVersion(releaseVersion);
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
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<int> GetNumberOfReleasesBehind(bool stableOnly = false, CancellationToken ct = default)
    {
        var updates = await GetAllReleases(ct: ct);
        if (updates.Count == 0) return 0;

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
    /// Clears the built release list and the cached GitHub responses
    /// </summary>
    /// <param name="ct"></param>
    public void BustGithubCache(CancellationToken ct = default)
    {
        _fileCacheService.Invalidate(ReleasesCacheKey, FileCacheService.VersionDirectory);
        // A /releases response cached before this version was published would make this build look like a nightly
        _githubApiService.ClearCache();
    }

    private UpdateNotificationDto? CreateDto(GithubReleaseMetadata? update)
    {
        if (update == null || string.IsNullOrEmpty(update.Tag_Name)) return null;
        if (!Version.TryParse(update.Tag_Name.Replace("v", string.Empty), out var updateVersion))
        {
            _logger.LogWarning("Skipping GitHub release with an unparseable tag {Tag}", update.Tag_Name);
            return null;
        }
        var currentVersion = BuildInfo.Version.ToString(4);


        var bodyHtml = Markdown.ToHtml(update.Body.Trim(), _markdownPipeline);
        var parsedSections = ParseReleaseBody(update.Body);
        var blogPart = Markdown.ToHtml(ExtractBlogPart(update.Body).Trim(), _markdownPipeline);

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


    public async Task PushUpdate(UpdateNotificationDto update, CancellationToken ct = default)
    {
        if (update == null) return;

        var updateVersion = new Version(update.UpdateVersion);

        if (BuildInfo.Version < updateVersion)
        {
            _logger.LogWarning("Server is out of date. Current: {CurrentVersion}. Available: {AvailableUpdate}", BuildInfo.Version, updateVersion);
            await _eventHub.SendMessageAsync(MessageFactory.UpdateAvailable, MessageFactory.UpdateVersionEvent(update),
                true, ct);
        }
    }

    private async Task<string?> GetVersionFromCommit(string commitSha, CancellationToken ct)
    {
        // Pinned to a commit sha, so the file never changes
        var csproj = await _githubApiService.DownloadRawAsync(KavitaRepo, commitSha, "Kavita.Common/Kavita.Common.csproj",
            TimeSpan.MaxValue, ct);
        if (!csproj.HasValue) return null;

        var versionMatch = Regex.Match(csproj.Value, @"<AssemblyVersion>([0-9\.]+)</AssemblyVersion>");
        return versionMatch.Success ? versionMatch.Groups[1].Value : null;
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
