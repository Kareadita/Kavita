using System.Collections.Generic;
using System.Linq;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;

namespace Kavita.Database.Extensions;

public static class KoboQueryExtensions
{

    // Note: This needs to be updated if we ever support auto conversion from CBZ to Epub
    private static readonly List<MangaFormat> SupportedFormats =
    [
        MangaFormat.Epub
    ];

    /// <param name="query"></param>
    extension(IQueryable<Chapter> query)
    {
        /// <summary>
        /// Filters the query to only return items that should be synced to the Kobo
        /// </summary>
        /// <param name="preferences"></param>
        /// <returns></returns>
        public IQueryable<Chapter> FilterByKoboSyncSettings(AppUserKoboSyncPreferences preferences)
        {
            if (preferences.IsSyncingCollection)
            {
                query = query.Where(c => c.Volume.Series.Collections.Any(c => c.Id == preferences.SyncCollection));
            }
            else if (!preferences.AllLibraries && preferences.SyncLibraries.Count != 0)
            {
                query = query.Where(c => preferences.SyncLibraries.Contains(c.Volume.Series.LibraryId));
            }

            return query.Where(c => SupportedFormats.Contains(c.Volume.Series.Format));
        }

        /// <summary>
        /// The inverse of <see cref="FilterByKoboSyncSettings"/>. I.e. only returns chapters not synced to the Kobo
        /// </summary>
        /// <param name="preferences"></param>
        /// <returns></returns>
        public IQueryable<Chapter> FilterAgainstKoboSyncSettings(AppUserKoboSyncPreferences preferences)
        {
            if (preferences.IsSyncingCollection)
            {
                // Using All seems to be break the query. Do not change
                query = query.Where(c => !c.Volume.Series.Collections.Any(uc => uc.Id == preferences.SyncCollection));
            }
            else if (!preferences.AllLibraries && preferences.SyncLibraries.Count != 0)
            {
                query = query.Where(c => !preferences.SyncLibraries.Contains(c.Volume.Series.LibraryId));
            }

            return query.Where(c => SupportedFormats.Contains(c.Volume.Series.Format));
        }
    }
}
