using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Interfaces.Services;
using API.SignalR;
using API.SignalR.Presence;
using Flurl.Http;
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
        public string Html_Url { get; init; }

    }
    public class VersionUpdaterService : IVersionUpdaterService
    {
        private readonly ILogger<VersionUpdaterService> _logger;
        private readonly IHubContext<MessageHub> _messageHub;
        private readonly IPresenceTracker _tracker;
        private readonly Markdown _markdown = new MarkdownDeep.Markdown();

        public VersionUpdaterService(ILogger<VersionUpdaterService> logger, IHubContext<MessageHub> messageHub, IPresenceTracker tracker)
        {
            _logger = logger;
            _messageHub = messageHub;
            _tracker = tracker;
        }

        /// <summary>
        /// Scheduled Task that checks if a newer version is available. If it is, will check if User is currently connected and push
        /// a message.
        /// </summary>
        public async Task CheckForUpdate()
        {

            var update = await GetGithubRelease();

            if (update == null || string.IsNullOrEmpty(update.Tag_Name)) return;

            var admins = await _tracker.GetOnlineAdmins();
            var version = update.Tag_Name.Replace("v", string.Empty);
            var updateVersion = new Version(version);
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

        private async Task SendEvent(GithubReleaseMetadata update, IReadOnlyList<string> admins)
        {
            var version = update.Tag_Name.Replace("v", string.Empty);
            var updateVersion = new Version(version);
            var connections = new List<string>();
            foreach (var admin in admins)
            {
                connections.AddRange(await _tracker.GetConnectionsForUser(admin));
            }

            await _messageHub.Clients.Users(admins).SendAsync("UpdateAvailable", new SignalRMessage
            {
                Name = "UpdateAvailable",
                Body = new
                {
                    CurrentVersion = version,
                    UpdateVersion = updateVersion.ToString(),
                    UpdateBody =  _markdown.Transform(update.Body.Trim()),
                    UpdateTitle = update.Name,
                    UpdateUrl = update.Html_Url,
                    IsDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker
                }
            });
        }

        private static async Task<GithubReleaseMetadata> GetGithubRelease()
        {
            var update = await "https://api.github.com/repos/Kareadita/Kavita/releases/latest"
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .GetJsonAsync<GithubReleaseMetadata>();

            return update;
        }
    }
}
