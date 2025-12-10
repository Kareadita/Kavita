using System;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Entities.History;
using API.Extensions;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

public static class ManualMigrateReadingProfiles
{
    public static async Task Migrate(DataContext context, ILogger<Program> logger)
    {
        if (await context.ManualMigrationHistory.AnyAsync(m => m.Name == "ManualMigrateReadingProfiles"))
        {
            return;
        }

        logger.LogCritical("Running ManualMigrateReadingProfiles migration - Please be patient, this may take some time. This is not an error");

        var users = await context.AppUser
            .Include(u => u.UserPreferences)
            .Include(u => u.ReadingProfiles)
            .ToListAsync();

        foreach (var user in users)
        {
            var readingProfile = new AppUserReadingProfile
            {
                Name = "Default",
                NormalizedName = "Default".ToNormalized(),
                Kind = ReadingProfileKind.Default,
                LibraryIds = [],
                SeriesIds = [],
                BackgroundColor = user.UserPreferences.BackgroundColor,
                EmulateBook = user.UserPreferences.EmulateBook,
                AppUser = user,
                PdfTheme = user.UserPreferences.PdfTheme,
                ReaderMode = user.UserPreferences.ReaderMode,
                ReadingDirection = user.UserPreferences.ReadingDirection,
                ScalingOption = user.UserPreferences.ScalingOption,
                LayoutMode = user.UserPreferences.LayoutMode,
                WidthOverride = null,
                AppUserId = user.Id,
                AutoCloseMenu = user.UserPreferences.AutoCloseMenu,
                BookReaderMargin = user.UserPreferences.BookReaderMargin,
                PageSplitOption = user.UserPreferences.PageSplitOption,
                BookThemeName = user.UserPreferences.BookThemeName,
                PdfSpreadMode = user.UserPreferences.PdfSpreadMode,
                PdfScrollMode = user.UserPreferences.PdfScrollMode,
                SwipeToPaginate = user.UserPreferences.SwipeToPaginate,
                BookReaderFontFamily = user.UserPreferences.BookReaderFontFamily,
                BookReaderFontSize = user.UserPreferences.BookReaderFontSize,
                BookReaderImmersiveMode = user.UserPreferences.BookReaderImmersiveMode,
                BookReaderLayoutMode = user.UserPreferences.BookReaderLayoutMode,
                BookReaderLineSpacing = user.UserPreferences.BookReaderLineSpacing,
                BookReaderReadingDirection = user.UserPreferences.BookReaderReadingDirection,
                BookReaderWritingStyle = user.UserPreferences.BookReaderWritingStyle,
                AllowAutomaticWebtoonReaderDetection = user.UserPreferences.AllowAutomaticWebtoonReaderDetection,
                BookReaderTapToPaginate = user.UserPreferences.BookReaderTapToPaginate,
                ShowScreenHints = user.UserPreferences.ShowScreenHints,
            };
            user.ReadingProfiles.Add(readingProfile);
        }

        await context.SaveChangesAsync();

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
