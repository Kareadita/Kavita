using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;



/// <summary>
/// Introduced in v0.8.0, this migrates the existing Chapter and Volume 0 -> Parser defined, MangaFile.FileName
/// </summary>
public static class MigrateChapterFields
{
    public static async Task Migrate(DataContext dataContext, IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateChapterFields"))
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateChapterFields migration - Please be patient, this may take some time. This is not an error");

        // Update all volumes only have specials in them (rare)
        var volumes = dataContext.Volume
            .Include(v => v.Chapters)
            .Where(v => v.Name == "0" && v.Chapters.All(c => c.IsSpecial))
            .ToList();
        foreach (var volume in volumes)
        {
            volume.Number = Parser.SpecialVolumeNumber;
            volume.MinNumber = Parser.SpecialVolumeNumber;
            volume.MaxNumber = Parser.SpecialVolumeNumber;
        }

        // Update all MangaFile
        foreach (var mangaFile in dataContext.MangaFile)
        {
            mangaFile.FileName = Path.GetFileNameWithoutExtension(mangaFile.FilePath);
        }

        foreach (var chapter in dataContext.Chapter.Where(c => c.Number == "0"))
        {
            chapter.Number = Parser.DefaultChapter;
            chapter.MinNumber = Parser.DefaultChapterNumber;
            chapter.MaxNumber = Parser.DefaultChapterNumber;
        }

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateChapterFields",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });

        await dataContext.SaveChangesAsync();
        logger.LogCritical(
            "Running MigrateChapterFields migration - Completed. This is not an error");
    }
}
