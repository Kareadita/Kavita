using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
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
        var volumesWithJustSpecials = dataContext.Volume
            .Include(v => v.Chapters)
            .Where(v => v.Name == "0" && v.Chapters.All(c => c.IsSpecial))
            .ToList();
        logger.LogCritical(
            "Running MigrateChapterFields migration - Updating {Count} volumes that only have specials in them", volumesWithJustSpecials.Count);
        foreach (var volume in volumesWithJustSpecials)
        {
            volume.Name = $"{Parser.SpecialVolumeNumber}";
            volume.MinNumber = Parser.SpecialVolumeNumber;
            volume.MaxNumber = Parser.SpecialVolumeNumber;
        }

        // Update all volumes that only have loose leafs in them
        var looseLeafVolumes = dataContext.Volume
            .Include(v => v.Chapters)
            .Where(v => v.Name == "0" && v.Chapters.All(c => !c.IsSpecial))
            .ToList();
        logger.LogCritical(
            "Running MigrateChapterFields migration - Updating {Count} volumes that only have loose leaf chapters in them", looseLeafVolumes.Count);
        foreach (var volume in looseLeafVolumes)
        {
            volume.Name = $"{Parser.DefaultChapterNumber}";
            volume.MinNumber = Parser.DefaultChapterNumber;
            volume.MaxNumber = Parser.DefaultChapterNumber;
        }

        // Update all MangaFile
        logger.LogCritical(
            "Running MigrateChapterFields migration - Updating all MangaFiles");
        foreach (var mangaFile in dataContext.MangaFile)
        {
            mangaFile.FileName = Parser.RemoveExtensionIfSupported(mangaFile.FilePath);
        }

        var looseLeafChapters = await dataContext.Chapter.Where(c => c.Number == "0").ToListAsync();
        logger.LogCritical(
            "Running MigrateChapterFields migration - Updating {Count} loose leaf chapters", looseLeafChapters.Count);
        foreach (var chapter in looseLeafChapters)
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
