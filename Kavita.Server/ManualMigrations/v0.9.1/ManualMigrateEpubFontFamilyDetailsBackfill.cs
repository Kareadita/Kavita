using System.Linq;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models;
using Kavita.Models.Entities.Enums.Font;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// v0.9.1 expanded the EpubFont to contain Family, Style and Weight properties
/// </summary>
public class ManualMigrateEpubFontFamilyDetailsBackfill : ManualMigration
{
    protected override string MigrationName => "ManualMigrateEpubFontFamilyDetailsBackfill";
    /// <summary>
    /// Migrates System fonts by finding the corresponding entry in the DefaultFonts list
    /// and mapping the added properties to the font in the database.
    ///
    /// For custom user fonts attempt a best effort guess by re-parsing the filename and
    /// applying updated results. If this best guess is incorrect then users will still
    /// need to delete and re-upload fonts while following filename guidelines if uploading
    /// from local files. Re-upload will most likely need to happen for fonts that were
    /// uploaded via a Google Fonts URL since we were previously not maintaining the
    /// filenames that were acquired from Google Fonts.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var fonts = await context.EpubFont.ToListAsync();
        if (fonts.Count == 0) return;

        var prefs = await context.AppUserReadingProfiles.ToListAsync();

        foreach (var font in fonts)
        {
            // Current font list at this stage of program execution already includes
            // newly seeded font variations that were added during development of
            // Font Family details changes.
            // Only update the system fonts that are missing items.
            if (font.Provider == FontProvider.System)
            {
                if (string.IsNullOrWhiteSpace(font.Family))
                {
                    // Find matching font in DefaultFont list via filename since that will be the same
                    var defaultFont = Defaults.DefaultFonts.FirstOrDefault(df => df.FileName == font.FileName);
                    if (defaultFont is not null)
                    {
                        // BookReaderFontFamily in Reading Profiles does not need to be updated
                        // here since the old fonts that already existed in DefaultFonts list
                        // were given the same string for Family and Name. This was done so that
                        // the SeedFonts function wouldn't erroneously create duplicate font entries
                        // in the database.

                        // Update items that are new based on current state of DefaultFonts list
                        font.Family = defaultFont.Family;
                        font.Style = defaultFont.Style;
                        font.Weight = defaultFont.Weight;
                    }
                }
            }
            else
            {
                // Attempt best guess upgrade based on current filename via re-parsing font
                var parsedFont = Parser.ParseEpubFontFromFilename(font.FileName);

                // Update Font Family preferences for Family instead of Name
                foreach (var pref in prefs)
                {
                    if (pref.BookReaderFontFamily == font.Name)
                    {
                        pref.BookReaderFontFamily = parsedFont.Family;
                    }
                }

                // Update items that are new or have changed based on parsing
                font.Name = parsedFont.Name;
                font.NormalizedName = parsedFont.NormalizedName;
                font.Family = parsedFont.Family;
                font.Style = parsedFont.Style;
                font.Weight = parsedFont.Weight;
            }
        }

        await context.SaveChangesAsync();

    }
}
