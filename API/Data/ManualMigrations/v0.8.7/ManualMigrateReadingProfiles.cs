using System;
using System.Threading.Tasks;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations.v0._8._7;

public class ManualMigrateReadingProfiles
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateReadingProfiles"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateReadingProfiles migration - Please be patient, this may take some time. This is not an error");

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO AppUserReadingProfiles (
                AppUserId,
                ReadingDirection,
                ScalingOption,
                PageSplitOption,
                ReaderMode,
                AutoCloseMenu,
                ShowScreenHints,
                EmulateBook,
                LayoutMode,
                BackgroundColor,
                SwipeToPaginate,
                AllowAutomaticWebtoonReaderDetection,
                BookReaderMargin,
                BookReaderLineSpacing,
                BookReaderFontSize,
                BookReaderFontFamily,
                BookReaderTapToPaginate,
                BookReaderReadingDirection,
                BookReaderWritingStyle,
                BookThemeName,
                BookReaderLayoutMode,
                BookReaderImmersiveMode,
                PdfTheme,
                PdfScrollMode,
                PdfSpreadMode
            )
            SELECT
                AppUserId,
                ReadingDirection,
                ScalingOption,
                PageSplitOption,
                ReaderMode,
                AutoCloseMenu,
                ShowScreenHints,
                EmulateBook,
                LayoutMode,
                BackgroundColor,
                SwipeToPaginate,
                AllowAutomaticWebtoonReaderDetection,
                BookReaderMargin,
                BookReaderLineSpacing,
                BookReaderFontSize,
                BookReaderFontFamily,
                BookReaderTapToPaginate,
                BookReaderReadingDirection,
                BookReaderWritingStyle,
                BookThemeName,
                BookReaderLayoutMode,
                BookReaderImmersiveMode,
                PdfTheme,
                PdfScrollMode,
                PdfSpreadMode
            FROM AppUserPreferences
        ");

        context.ManualMigrationHistory.Add(new ManualMigrationHistory
        {
            Name = "ManualMigrateReadingProfiles",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();


        logger.LogCritical("Running ManualMigrateReadingProfiles migration - Completed. This is not an error");

    }
}
