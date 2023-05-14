using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

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
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE Series SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc'),
                       [LastChapterAddedUtc] = datetime([LastChapterAdded], 'utc'),
                       [LastFolderScannedUtc] = datetime([LastFolderScanned], 'utc')
                ;
            ");
        logger.LogInformation("Updating Dates on Series...Done");
        #endregion

        #region Library
        logger.LogInformation("Updating Dates on Libraries...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE Library SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc')
                ;
            ");
        logger.LogInformation("Updating Dates on Libraries...Done");
        #endregion

        #region Volume
        try
        {
            logger.LogInformation("Updating Dates on Volumes...");
            await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE Volume SET
                      [LastModifiedUtc] = datetime([LastModified], 'utc'),
                      [CreatedUtc] = datetime([Created], 'utc');
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
                UPDATE Chapter SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc')
                ;
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
                UPDATE AppUserBookmark SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc')
                ;
            ");
        logger.LogInformation("Updating Dates on Bookmarks...Done");
        #endregion

        #region AppUserProgress
        logger.LogInformation("Updating Dates on Progress...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE AppUserProgresses SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc')
                ;
            ");
        logger.LogInformation("Updating Dates on Progress...Done");
        #endregion

        #region Device
        logger.LogInformation("Updating Dates on Device...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE Device SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc'),
                       [LastUsedUtc] = datetime([LastUsed], 'utc')
                ;
            ");
        logger.LogInformation("Updating Dates on Device...Done");
        #endregion

        #region MangaFile
        logger.LogInformation("Updating Dates on MangaFile...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE MangaFile SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc'),
                       [LastFileAnalysisUtc] = datetime([LastFileAnalysis], 'utc')
                ;
            ");
        logger.LogInformation("Updating Dates on MangaFile...Done");
        #endregion

        #region ReadingList
        logger.LogInformation("Updating Dates on ReadingList...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE ReadingList SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc')
                ;
            ");
        logger.LogInformation("Updating Dates on ReadingList...Done");
        #endregion

        #region SiteTheme
        logger.LogInformation("Updating Dates on SiteTheme...");
        await dataContext.Database.ExecuteSqlRawAsync(@"
                UPDATE SiteTheme SET
                       [LastModifiedUtc] = datetime([LastModified], 'utc'),
                       [CreatedUtc] = datetime([Created], 'utc')
                ;
            ");
        logger.LogInformation("Updating Dates on SiteTheme...Done");
        #endregion

        logger.LogInformation("MigrateToUtcDates migration finished");

    }
}
