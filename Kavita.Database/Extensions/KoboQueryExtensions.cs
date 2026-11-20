using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;

namespace Kavita.Database.Extensions;

public static class KoboQueryExtensions
{

    private static readonly List<MangaFormat> SupportedFormats =
    [
        MangaFormat.Epub, MangaFormat.Archive, MangaFormat.Image
    ];

    /// <param name="query"></param>
    extension(IQueryable<Chapter> query)
    {
        /// <summary>
        /// Filters the query to only return items that should be synced to the Kobo
        /// </summary>
        /// <param name="preferences"></param>
        /// <returns></returns>
        public IQueryable<Chapter> FilterByKoboSyncSettings(AppUserKoboSyncPreferences preferences, int userId)
        {
            query = query.Where(c => SupportedFormats.Contains(c.Volume.Series.Format));

            var sources = new List<Expression<Func<Chapter, bool>>>();

            if (preferences.LibrarySyncEnabled)
            {
                if (preferences.SyncLibraries.Count > 0)
                {
                    sources.Add(c => preferences.SyncLibraries.Contains(c.Volume.Series.LibraryId));
                }
                else // everything
                {
                    return query;
                }
            }

            if (preferences.ReadingListSyncEnabled)
            {
                if (preferences.SyncReadingLists.Count > 0)
                {
                    sources.Add(c => c.ReadingListItems.Any(item => preferences.SyncReadingLists.Contains(item.ReadingListId)));
                }
                else
                {
                    sources.Add(c => c.ReadingListItems.Any(item => item.ReadingList.AppUserId == userId));
                }
            }

            if (preferences.CollectionSyncEnabled)
            {
                if (preferences.SyncCollections.Count > 0)
                {
                    sources.Add(c => c.Volume.Series.Collections.Any(col => preferences.SyncCollections.Contains(col.Id) || col.Promoted));
                }
                else
                {
                    sources.Add(c => c.Volume.Series.Collections.Any(col => col.AppUserId == userId || col.Promoted));
                }
            }

            if (preferences.HasManagedCollection)
            {
                sources.Add(c => c.Volume.Series.Collections.Any(col => col.Id == preferences.ManagedCollection));
            }

            if (sources.Count == 0)
            {
                return query.Where(_ => false);
            }

            return query.Where(sources.Aggregate((a, b) => a.Or(b)));
        }

    }
}
