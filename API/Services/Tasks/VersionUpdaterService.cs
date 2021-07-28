using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using API.Interfaces.Services;
using Flurl;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    internal class GithubReleaseMetadata
    {
        /// <summary>
        /// Name of the Tag
        /// <example>v0.4.3</example>
        /// </summary>
        [JsonPropertyName("tag_name")]
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

        public VersionUpdaterService(ILogger<VersionUpdaterService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Scheduled Task that checks if a newer version is available. If it is, will check if User is currently connected and push
        /// a message.
        /// </summary>
        public async Task CheckForUpdate()
        {
            var update = await "https://api.github.com/repos/Kareadita/Kavita/releases/latest"
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .GetJsonAsync<GithubReleaseMetadata>();

            if (update != null && !string.IsNullOrEmpty(update.Tag_Name))
            {
                var version = update.Tag_Name.Replace("v", string.Empty);
                var updateVersion = new Version(version);
                if (BuildInfo.Version < updateVersion)
                {
                    _logger.LogInformation("Server is out of date. Current: {CurrentVersion}. Available: {AvailableUpdate}", BuildInfo.Version, updateVersion);
                }
                else
                {
                    _logger.LogInformation("Server is up to date. Current: {CurrentVersion}", BuildInfo.Version);
                }
            }
        }
    }
}
