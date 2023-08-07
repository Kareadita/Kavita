using System;
using System.Linq;
using System.Threading.Tasks;
using API.Services.Tasks;

namespace API.Data.ManualMigrations;

/// <summary>
/// In v0.5.3, we removed Light and E-Ink themes. This migration will remove the themes from the DB and default anyone on
/// null, E-Ink, or Light to Dark.
/// </summary>
public static class MigrateRemoveExtraThemes
{
    public static async Task Migrate(IUnitOfWork unitOfWork, IThemeService themeService)
    {
        var themes = (await unitOfWork.SiteThemeRepository.GetThemes()).ToList();

        if (themes.Find(t => t.Name.Equals("Light")) == null)
        {
            return;
        }

        Console.WriteLine("Removing Dark and E-Ink themes");

        var darkTheme = themes.Single(t => t.Name.Equals("Dark"));
        var lightTheme = themes.Single(t => t.Name.Equals("Light"));
        var eInkTheme = themes.Single(t => t.Name.Equals("E-Ink"));



        // Update default theme if it's not Dark or a custom theme
        await themeService.UpdateDefault(darkTheme.Id);

        // Update all users to Dark theme if they are on Light/E-Ink
        foreach (var pref in await unitOfWork.UserRepository.GetAllPreferencesByThemeAsync(lightTheme.Id))
        {
            pref.Theme = darkTheme;
        }
        foreach (var pref in await unitOfWork.UserRepository.GetAllPreferencesByThemeAsync(eInkTheme.Id))
        {
            pref.Theme = darkTheme;
        }

        // Remove Light/E-Ink themes
        foreach (var siteTheme in themes.Where(t => t.Name.Equals("Light") || t.Name.Equals("E-Ink")))
        {
            unitOfWork.SiteThemeRepository.Remove(siteTheme);
        }
        // Commit and call it a day
        await unitOfWork.CommitAsync();

        Console.WriteLine("Completed removing Dark and E-Ink themes");
    }

}
