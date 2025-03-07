using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.History;
using API.Services;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;


/// <summary>
/// v0.8.5 - Progress is extracted and saved in a csv since PDF parser has massive changes
/// </summary>
public static class MigrateProgressExportForV085
{
    public static async Task Migrate(DataContext dataContext, IDirectoryService directoryService, ILogger<Program> logger)
    {
        try
        {
            if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateProgressExportForV085"))
            {
                return;
            }

            logger.LogCritical(
                "Running MigrateProgressExportForV085 migration - Please be patient, this may take some time. This is not an error");

            var data = await dataContext.AppUserProgresses
                .Join(dataContext.Series, progress => progress.SeriesId, series => series.Id, (progress, series) => new { progress, series })
                .Join(dataContext.Volume, ps => ps.progress.VolumeId, volume => volume.Id, (ps, volume) => new { ps.progress, ps.series, volume })
                .Join(dataContext.Chapter, psv => psv.progress.ChapterId, chapter => chapter.Id, (psv, chapter) => new { psv.progress, psv.series, psv.volume, chapter })
                .Join(dataContext.MangaFile, psvc => psvc.chapter.Id, mangaFile => mangaFile.ChapterId, (psvc, mangaFile) => new { psvc.progress, psvc.series, psvc.volume, psvc.chapter, mangaFile })
                .Join(dataContext.AppUser, psvcm => psvcm.progress.AppUserId, appUser => appUser.Id, (psvcm, appUser) => new
                {
                    LibraryId = psvcm.series.LibraryId,
                    LibraryName = psvcm.series.Library.Name,
                    SeriesName = psvcm.series.Name,
                    VolumeRange = psvcm.volume.MinNumber + "-" + psvcm.volume.MaxNumber,
                    VolumeLookupName = psvcm.volume.Name,
                    ChapterRange = psvcm.chapter.Range,
                    MangaFileName = psvcm.mangaFile.FileName,
                    MangaFilePath = psvcm.mangaFile.FilePath,
                    AppUserName = appUser.UserName,
                    AppUserId = appUser.Id,
                    PagesRead = psvcm.progress.PagesRead,
                    BookScrollId = psvcm.progress.BookScrollId,
                    ProgressCreated = psvcm.progress.Created,
                    ProgressLastModified = psvcm.progress.LastModified
                }).ToListAsync();


            // Write the mapped data to a CSV file
            await using var writer = new StreamWriter(Path.Join(directoryService.ConfigDirectory, "progress_export-v0.8.5.csv"));
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(data);

            logger.LogCritical(
                "Running MigrateProgressExportForV085 migration - Completed. This is not an error");
        }
        catch (Exception ex)
        {
            // On new installs, the db isn't setup yet, so this has nothing to do
        }

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateProgressExportForV085",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await dataContext.SaveChangesAsync();
    }
}
