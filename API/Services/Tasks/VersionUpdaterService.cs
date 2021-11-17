using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using API.DTOs.Update;
using API.Interfaces.Services;
using API.SignalR;
using API.SignalR.Presence;
using Flurl.Http;
using Flurl.Http.Configuration;
using Kavita.Common.EnvironmentInfo;
using MarkdownDeep;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    internal class GithubReleaseMetadata
    {
        /// <summary>
        /// Name of the Tag
        /// <example>v0.4.3</example>
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string Tag_Name { get; init; }
        /// <summary>
        /// Name of the Release
        /// </summary>
        public string Name { get; init; }
        /// <summary>
        /// Body of the Release
        /// </summary>
        public string Body { get; init; }
        /// <summary>
        /// Url of the release on Github
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string Html_Url { get; init; }
        /// <summary>
        /// Date Release was Published
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string Published_At { get; init; }
    }

    public class UntrustedCertClientFactory : DefaultHttpClientFactory
    {
        public override HttpMessageHandler CreateMessageHandler() {
            return new HttpClientHandler {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
        }
    }

    public class VersionUpdaterService : IVersionUpdaterService
    {
        private readonly ILogger<VersionUpdaterService> _logger;
        private readonly IHubContext<MessageHub> _messageHub;
        private readonly IPresenceTracker _tracker;
        private readonly Markdown _markdown = new MarkdownDeep.Markdown();
#pragma warning disable S1075
        private static readonly string GithubLatestReleasesUrl = "https://api.github.com/repos/Kareadita/Kavita/releases/latest";
        private static readonly string GithubAllReleasesUrl = "https://api.github.com/repos/Kareadita/Kavita/releases";
#pragma warning restore S1075

        public VersionUpdaterService(ILogger<VersionUpdaterService> logger, IHubContext<MessageHub> messageHub, IPresenceTracker tracker)
        {
            _logger = logger;
            _messageHub = messageHub;
            _tracker = tracker;

            FlurlHttp.ConfigureClient(GithubLatestReleasesUrl, cli =>
                cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
            FlurlHttp.ConfigureClient(GithubAllReleasesUrl, cli =>
                cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
        }

        /// <summary>
        /// Fetches the latest release from Github
        /// </summary>
        public async Task<UpdateNotificationDto> CheckForUpdate()
        {
            var update = await GetGithubRelease();
            return CreateDto(update);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<UpdateNotificationDto>> GetAllReleases()
        {
            var updates = await GetGithubReleases();
            return updates.Select(CreateDto);
        }

        private UpdateNotificationDto CreateDto(GithubReleaseMetadata update)
        {
            if (update == null || string.IsNullOrEmpty(update.Tag_Name)) return null;
            var updateVersion = new Version(update.Tag_Name.Replace("v", string.Empty));
            var currentVersion = BuildInfo.Version.ToString();

            if (updateVersion.Revision == -1)
            {
                currentVersion = currentVersion.Substring(0, currentVersion.LastIndexOf(".", StringComparison.Ordinal));
            }

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

        public async Task PushUpdate(UpdateNotificationDto update)
        {
            if (update == null) return;

            var admins = await _tracker.GetOnlineAdmins();
            var updateVersion = new Version(update.CurrentVersion);

            if (BuildInfo.Version < updateVersion)
            {
                _logger.LogInformation("Server is out of date. Current: {CurrentVersion}. Available: {AvailableUpdate}", BuildInfo.Version, updateVersion);
                await SendEvent(update, admins);
            }
            else if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development)
            {
                _logger.LogInformation("Server is up to date. Current: {CurrentVersion}", BuildInfo.Version);
                await SendEvent(update, admins);
            }
        }

        private async Task SendEvent(UpdateNotificationDto update, IReadOnlyList<string> admins)
        {
            var connections = new List<string>();
            foreach (var admin in admins)
            {
                connections.AddRange(await _tracker.GetConnectionsForUser(admin));
            }

            await _messageHub.Clients.Users(admins).SendAsync(SignalREvents.UpdateVersion, MessageFactory.UpdateVersionEvent(update));
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
}
