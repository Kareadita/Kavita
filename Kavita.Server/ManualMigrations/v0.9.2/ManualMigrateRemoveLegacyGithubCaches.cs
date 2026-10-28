using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Database;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

/// <summary>
/// GitHub responses are now cached by GithubApiService under cache-long/github. Removes the caches the version and CBL
/// services kept on their own, including the cache-long root copies ManualMigrateVersionCacheFiles left behind
/// </summary>
public class ManualMigrateRemoveLegacyGithubCaches(IDirectoryService directoryService) : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateRemoveLegacyGithubCaches);

    protected override Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var cacheLong = directoryService.LongTermCacheDirectory;

        try
        {
            // github_* also matches github_*.json.<guid>.tmp files left by writes whose rename failed
            directoryService.DeleteFiles(directoryService.FileSystem.Directory.GetFiles(cacheLong, "github_*")
                .Append(Path.Combine(cacheLong, "cbl-repo-cache.json")));
            directoryService.ClearAndDeleteDirectory(Path.Combine(cacheLong, "update"));
            directoryService.ClearAndDeleteDirectory(Path.Combine(cacheLong, "pr_cache"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not remove the old GitHub cache files from cache-long. They are unused and can be deleted manually");
        }

        return Task.CompletedTask;
    }
}
