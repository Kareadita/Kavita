using System;
using System.IO;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Database;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// v0.9.1 - Moved the cache long files for Version service to a scoped (update) directory. Move any files the user might already have.
/// </summary>
public class ManualMigrateVersionCacheFiles(IDirectoryService directoryService) : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateVersionCacheFiles);
    protected override Task ExecuteAsync(DataContext context, ILogger logger)
    {
        var updateScopePath = Path.Combine(directoryService.LongTermCacheDirectory, "update");

        var files = new string[]
        {
            Path.Combine(directoryService.LongTermCacheDirectory, "github_releases_cache.json"),
            Path.Combine(directoryService.LongTermCacheDirectory, "github_latest_release_cache.json"),
            Path.Combine(directoryService.LongTermCacheDirectory, "github_nightly_cache.json"),
            Path.Combine(directoryService.LongTermCacheDirectory, "github_commits_cache.json"),
        };

        try
        {
            directoryService.ExistOrCreate(updateScopePath);

            foreach (var file in files)
            {
                if (directoryService.FileSystem.File.Exists(file))
                    directoryService.CopyFileToDirectory(file, updateScopePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Encountered an error moving Version Update cache files. You can remove the github_*.json files from cache-long manually.");
        }

        // Move the pr_cache directory
        try
        {
            directoryService.ExistOrCreate(updateScopePath);

            var prCacheDirectory = Path.Combine(directoryService.LongTermCacheDirectory, "pr_cache");
            if (directoryService.Exists(prCacheDirectory))
            {
                directoryService.CopyDirectoryToDirectory(prCacheDirectory, updateScopePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Encountered an error moving Version Update cache files. You can remove the github_*.json files from cache-long manually.");
        }

        return Task.CompletedTask;
    }
}
