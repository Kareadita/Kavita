using System.Linq;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;

namespace Kavita.Database.Extensions;

public static class KoboQueryExtensions
{

    public static IQueryable<Chapter> FilterByKoboSyncSettings(this IQueryable<Chapter> query, AppUserKoboSyncPreferences preferences)
    {
        if (preferences.SyncCollection != 0)
        {
            query = query.Where(c => c.Volume.Series.Collections.Any(c => c.Id == preferences.SyncCollection));
        }
        else if (!preferences.AllLibraries && preferences.SyncLibraries.Any())
        {
            query = query.Where(c => preferences.SyncLibraries.Contains(c.Volume.Series.LibraryId));
        }

        // Note: This needs to be updated if we ever support auto conversion from CBZ to Epub
        return query.Where(c => c.Volume.Series.Format == MangaFormat.Epub);
    }

}
