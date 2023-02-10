using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data;

/// <summary>
/// Introduced in v0.6.1.38 or v0.7.0,
/// </summary>
public static class MigrateToUtcDates
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        // if current version is > 0.6.1.38, then we can exit and not perform
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (Version.Parse(settings.InstallVersion) > new Version(0, 6, 1, 38))
        {
            return;
        }
        logger.LogCritical("Running MigrateToUtcDates migration. Please be patient, this may take some time depending on the size of your library. Do not abort, this can break your Database");

        #region Series
        logger.LogInformation("Updating Dates on Series...");
        foreach (var series in await dataContext.Series.ToListAsync())
        {
            series.LastModifiedUtc = series.LastModified.ToUniversalTime();
            series.CreatedUtc = series.Created.ToUniversalTime();
            series.LastChapterAddedUtc = series.LastChapterAdded.ToUniversalTime();
            series.LastFolderScannedUtc = series.LastFolderScanned.ToUniversalTime();
            unitOfWork.SeriesRepository.Update(series);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on Series...Done");
        #endregion

        #region Library
        logger.LogInformation("Updating Dates on Libraries...");
        foreach (var library in await dataContext.Library.ToListAsync())
        {
            library.CreatedUtc = library.Created.ToUniversalTime();
            library.LastModifiedUtc = library.LastModified.ToUniversalTime();
            unitOfWork.LibraryRepository.Update(library);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on Libraries...Done");
        #endregion

        #region Volume
        logger.LogInformation("Updating Dates on Volumes...");
        foreach (var volume in await dataContext.Volume.ToListAsync())
        {
            volume.CreatedUtc = volume.Created.ToUniversalTime();
            volume.LastModifiedUtc = volume.LastModified.ToUniversalTime();
            unitOfWork.VolumeRepository.Update(volume);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on Volumes...Done");
        #endregion

        #region Chapter
        logger.LogInformation("Updating Dates on Chapters...");
        foreach (var chapter in await dataContext.Chapter.ToListAsync())
        {
            chapter.CreatedUtc = chapter.Created.ToUniversalTime();
            chapter.LastModifiedUtc = chapter.LastModified.ToUniversalTime();
            unitOfWork.ChapterRepository.Update(chapter);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on Chapters...Done");
        #endregion

        #region AppUserBookmark
        logger.LogInformation("Updating Dates on Bookmarks...");
        foreach (var bookmark in await dataContext.AppUserBookmark.ToListAsync())
        {
            bookmark.CreatedUtc = bookmark.Created.ToUniversalTime();
            bookmark.LastModifiedUtc = bookmark.LastModified.ToUniversalTime();
            dataContext.Entry(bookmark).State = EntityState.Modified;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on Bookmarks...Done");
        #endregion

        #region AppUserProgress
        logger.LogInformation("Updating Dates on Progress...");
        foreach (var progress in await dataContext.AppUserProgresses.ToListAsync())
        {
            progress.CreatedUtc = progress.Created.ToUniversalTime();
            progress.LastModifiedUtc = progress.LastModified.ToUniversalTime();
            dataContext.Entry(progress).State = EntityState.Modified;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on Progress...Done");
        #endregion

        #region Device
        logger.LogInformation("Updating Dates on Device...");
        foreach (var device in await dataContext.Device.ToListAsync())
        {
            device.CreatedUtc = device.Created.ToUniversalTime();
            device.LastModifiedUtc = device.LastModified.ToUniversalTime();
            device.LastUsedUtc = device.LastUsed.ToUniversalTime();
            dataContext.Entry(device).State = EntityState.Modified;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on Device...Done");
        #endregion

        #region MangaFile
        logger.LogInformation("Updating Dates on MangaFile...");
        foreach (var file in await dataContext.MangaFile.ToListAsync())
        {
            file.CreatedUtc = file.Created.ToUniversalTime();
            file.LastModifiedUtc = file.LastModified.ToUniversalTime();
            file.LastFileAnalysisUtc = file.LastFileAnalysis.ToUniversalTime();
            dataContext.Entry(file).State = EntityState.Modified;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on MangaFile...Done");
        #endregion

        #region ReadingList
        logger.LogInformation("Updating Dates on ReadingList...");
        foreach (var readingList in await dataContext.ReadingList.ToListAsync())
        {
            readingList.CreatedUtc = readingList.Created.ToUniversalTime();
            readingList.LastModifiedUtc = readingList.LastModified.ToUniversalTime();
            dataContext.Entry(readingList).State = EntityState.Modified;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on ReadingList...Done");
        #endregion

        #region SiteTheme
        logger.LogInformation("Updating Dates on SiteTheme...");
        foreach (var theme in await dataContext.SiteTheme.ToListAsync())
        {
            theme.CreatedUtc = theme.Created.ToUniversalTime();
            theme.LastModifiedUtc = theme.LastModified.ToUniversalTime();
            dataContext.Entry(theme).State = EntityState.Modified;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Dates on SiteTheme...Done");
        #endregion

        logger.LogInformation("MigrateToUtcDates migration finished");

    }
}
