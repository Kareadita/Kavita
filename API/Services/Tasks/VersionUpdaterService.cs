﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Update;
using API.SignalR;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using MarkdownDeep;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;

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
    /// Url of the release on Github
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
    Task<IEnumerable<UpdateNotificationDto>> GetAllReleases();
}

public class VersionUpdaterService : IVersionUpdaterService
{
    private readonly ILogger<VersionUpdaterService> _logger;
    private readonly IEventHub _eventHub;
    private readonly Markdown _markdown = new MarkdownDeep.Markdown();
#pragma warning disable S1075
    private const string GithubLatestReleasesUrl = "https://api.github.com/repos/Kareadita/Kavita/releases/latest";
    private const string GithubAllReleasesUrl = "https://api.github.com/repos/Kareadita/Kavita/releases";
#pragma warning restore S1075

    public VersionUpdaterService(ILogger<VersionUpdaterService> logger, IEventHub eventHub)
    {
        _logger = logger;
        _eventHub = eventHub;

        FlurlHttp.ConfigureClient(GithubLatestReleasesUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
        FlurlHttp.ConfigureClient(GithubAllReleasesUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    /// <summary>
    /// Fetches the latest release from Github
    /// </summary>
    /// <returns>Latest update or null if current version is greater than latest update</returns>
    public async Task<UpdateNotificationDto?> CheckForUpdate()
    {
        var update = await GetGithubRelease();
        var dto = CreateDto(update);
        if (dto == null) return null;
        return new Version(dto.UpdateVersion) <= new Version(dto.CurrentVersion) ? null : dto;
    }

    public async Task<IEnumerable<UpdateNotificationDto>> GetAllReleases()
    {
        var updates = await GetGithubReleases();
        return updates.Select(CreateDto).Where(d => d != null)!;
    }

    private UpdateNotificationDto? CreateDto(GithubReleaseMetadata? update)
    {
        if (update == null || string.IsNullOrEmpty(update.Tag_Name)) return null;
        var updateVersion = new Version(update.Tag_Name.Replace("v", string.Empty));
        var currentVersion = BuildInfo.Version.ToString(4);

        return new UpdateNotificationDto()
        {
            CurrentVersion = currentVersion,
            UpdateVersion = updateVersion.ToString(),
            UpdateBody = _markdown.Transform(update.Body.Trim()),
            UpdateTitle = update.Name,
            UpdateUrl = update.Html_Url,
            IsDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker,
            PublishDate = update.Published_At
        };
    }

    public async Task PushUpdate(UpdateNotificationDto? update)
    {
        if (update == null) return;

        var updateVersion = new Version(update.CurrentVersion);

        if (BuildInfo.Version < updateVersion)
        {
            _logger.LogInformation("Server is out of date. Current: {CurrentVersion}. Available: {AvailableUpdate}", BuildInfo.Version, updateVersion);
            await _eventHub.SendMessageAsync(MessageFactory.UpdateAvailable, MessageFactory.UpdateVersionEvent(update),
                true);
        }
        else if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development)
        {
            _logger.LogInformation("Server is up to date. Current: {CurrentVersion}", BuildInfo.Version);
            await _eventHub.SendMessageAsync(MessageFactory.UpdateAvailable, MessageFactory.UpdateVersionEvent(update),
                true);
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

    private static async Task<IEnumerable<GithubReleaseMetadata>> GetGithubReleases()
    {
        var update = await GithubAllReleasesUrl
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .GetJsonAsync<IEnumerable<GithubReleaseMetadata>>();

        return update;
    }
}
