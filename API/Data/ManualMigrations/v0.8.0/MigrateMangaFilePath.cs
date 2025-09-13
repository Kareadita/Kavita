using System;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.0 ensured that MangaFile Path is normalized. This will normalize existing data to avoid churn.
/// </summary>
public static class MigrateMangaFilePath
{
    public static async Task Migrate(DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateMangaFilePath"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateMangaFilePath migration - Please be patient, this may take some time. This is not an error");


        foreach(var file in dataContext.MangaFile)
        {
            file.FilePath = Parser.NormalizePath(file.FilePath);
        }

        await dataContext.SaveChangesAsync();

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateMangaFilePath",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await dataContext.SaveChangesAsync();

        logger.LogCritical(
            "Running MigrateMangaFilePath migration - Completed. This is not an error");
    }
}
