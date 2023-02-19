using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data;

/// <summary>
/// v0.7 introduced UTC dates and GMT+1 users would sometimes have dates stored as '0000-12-31 23:00:00'.
/// This Migration will update those dates.
/// </summary>
public static class MigrateBrokenGMT1Dates
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        // if current version is > 0.7, then we can exit and not perform
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (Version.Parse(settings.InstallVersion) > new Version(0, 7, 0, 2))
        {
            return;
        }
        logger.LogCritical("Running MigrateToUtcDates migration. Please be patient, this may take some time depending on the size of your library. Do not abort, this can break your Database");

        #region Series
        logger.LogInformation("Updating Dates on Series...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE Series SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
                UPDATE Series SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
                UPDATE Series SET LastChapterAddedUtc = '0001-01-01 00:00:00' WHERE LastChapterAddedUtc = '0000-12-31 23:00:00';
                UPDATE Series SET LastFolderScannedUtc = '0001-01-01 00:00:00' WHERE LastFolderScannedUtc = '0000-12-31 23:00:00';
            ");
        logger.LogInformation("Updating Dates on Series...Done");
        #endregion

        #region Library
        logger.LogInformation("Updating Dates on Libraries...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE Library SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
                UPDATE Library SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            ");
        logger.LogInformation("Updating Dates on Libraries...Done");
        #endregion

        #region Volume
        try
        {
            logger.LogInformation("Updating Dates on Volumes...");
            await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE Volume SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
                UPDATE Volume SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            ");
            logger.LogInformation("Updating Dates on Volumes...Done");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Updating Dates on Volumes...Failed");
        }
        #endregion

        #region Chapter
        try
        {
            logger.LogInformation("Updating Dates on Chapters...");
            await dataContext.Database.ExecuteSqlRawAsync(@"
            UPDATE Chapter SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
            UPDATE Chapter SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            ");
            logger.LogInformation("Updating Dates on Chapters...Done");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Updating Dates on Chapters...Failed");
        }
        #endregion

        #region AppUserBookmark
        logger.LogInformation("Updating Dates on Bookmarks...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
            UPDATE AppUserBookmark SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
            UPDATE AppUserBookmark SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            ");
        logger.LogInformation("Updating Dates on Bookmarks...Done");
        #endregion

        #region AppUserProgress
        logger.LogInformation("Updating Dates on Progress...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
            UPDATE AppUserProgresses SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
            UPDATE AppUserProgresses SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            ");
        logger.LogInformation("Updating Dates on Progress...Done");
        #endregion

        #region Device
        logger.LogInformation("Updating Dates on Device...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
            UPDATE Device SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
            UPDATE Device SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            UPDATE Device SET LastUsedUtc = '0001-01-01 00:00:00' WHERE LastUsedUtc = '0000-12-31 23:00:00';
            ");
        logger.LogInformation("Updating Dates on Device...Done");
        #endregion

        #region MangaFile
        logger.LogInformation("Updating Dates on MangaFile...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
            UPDATE MangaFile SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
            UPDATE MangaFile SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            UPDATE MangaFile SET LastFileAnalysisUtc = '0001-01-01 00:00:00' WHERE LastFileAnalysisUtc = '0000-12-31 23:00:00';
            ");
        logger.LogInformation("Updating Dates on MangaFile...Done");
        #endregion

        #region ReadingList
        logger.LogInformation("Updating Dates on ReadingList...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
            UPDATE ReadingList SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
            UPDATE ReadingList SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            ");
        logger.LogInformation("Updating Dates on ReadingList...Done");
        #endregion

        #region SiteTheme
        logger.LogInformation("Updating Dates on SiteTheme...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
            UPDATE SiteTheme SET CreatedUtc = '0001-01-01 00:00:00' WHERE CreatedUtc = '0000-12-31 23:00:00';
            UPDATE SiteTheme SET LastModifiedUtc = '0001-01-01 00:00:00' WHERE LastModifiedUtc = '0000-12-31 23:00:00';
            ");
        logger.LogInformation("Updating Dates on SiteTheme...Done");
        #endregion

        logger.LogInformation("MigrateToUtcDates migration finished");

    }
}
