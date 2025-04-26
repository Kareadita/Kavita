using System.Linq;
using API.Data.Repositories;
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions;
#nullable enable

/// <summary>
/// All extensions against IQueryable that enables the dynamic including based on bitwise flag pattern
/// </summary>
public static class IncludesExtensions
{
    public static IQueryable<CollectionTag> Includes(this IQueryable<CollectionTag> queryable,
        CollectionTagIncludes includes)
    {
        if (includes.HasFlag(CollectionTagIncludes.SeriesMetadata))
        {
            queryable = queryable.Include(c => c.SeriesMetadatas);
        }

        if (includes.HasFlag(CollectionTagIncludes.SeriesMetadataWithSeries))
        {
            queryable = queryable.Include(c => c.SeriesMetadatas).ThenInclude(s => s.Series);
        }

        return queryable.AsSplitQuery();
    }

    public static IQueryable<AppUserCollection> Includes(this IQueryable<AppUserCollection> queryable,
        CollectionIncludes includes)
    {
        if (includes.HasFlag(CollectionIncludes.Series))
        {
            queryable = queryable.Include(c => c.Items);
        }


        return queryable.AsSplitQuery();
    }

    public static IQueryable<Chapter> Includes(this IQueryable<Chapter> queryable,
        ChapterIncludes includes)
    {
        if (includes.HasFlag(ChapterIncludes.Volumes))
        {
            queryable = queryable.Include(v => v.Volume);
        }

        if (includes.HasFlag(ChapterIncludes.Files))
        {
            queryable = queryable
                .Include(c => c.Files);
        }

        if (includes.HasFlag(ChapterIncludes.People))
        {
            queryable = queryable
                .Include(c => c.People)
                .ThenInclude(cp => cp.Person);
        }

        if (includes.HasFlag(ChapterIncludes.Genres))
        {
            queryable = queryable
                .Include(c => c.Genres);
        }

        if (includes.HasFlag(ChapterIncludes.Tags))
        {
            queryable = queryable
                .Include(c => c.Tags);
        }

        return queryable.AsSplitQuery();
    }

    public static IQueryable<Volume> Includes(this IQueryable<Volume> queryable,
        VolumeIncludes includes)
    {
        if (includes.HasFlag(VolumeIncludes.Files))
        {
            queryable = queryable
                .Include(vol => vol.Chapters)
                .ThenInclude(c => c.Files);
        } else if (includes.HasFlag(VolumeIncludes.Chapters))
        {
            queryable = queryable
                .Include(vol => vol.Chapters);
        }

        if (includes.HasFlag(VolumeIncludes.People))
        {
            queryable = queryable
                .Include(vol => vol.Chapters)
                .ThenInclude(c => c.People);
        }

        if (includes.HasFlag(VolumeIncludes.Tags))
        {
            queryable = queryable
                .Include(vol => vol.Chapters)
                .ThenInclude(c => c.Tags);
        }

        return queryable.AsSplitQuery();
    }

    public static IQueryable<Series> Includes(this IQueryable<Series> query,
        SeriesIncludes includeFlags)
    {
        if (includeFlags.HasFlag(SeriesIncludes.Library))
        {
            query = query.Include(u => u.Library);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Volumes))
        {
            query = query.Include(s => s.Volumes);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Chapters))
        {
            query = query
                .Include(s => s.Volumes)
                .ThenInclude(v => v.Chapters.OrderBy(c => c.SortOrder));
        }

        if (includeFlags.HasFlag(SeriesIncludes.Related))
        {
            query = query.Include(s => s.Relations)
                .ThenInclude(r => r.TargetSeries)
                .Include(s => s.RelationOf);
        }

        if (includeFlags.HasFlag(SeriesIncludes.ExternalReviews))
        {
            query = query
                .Include(s => s.ExternalSeriesMetadata)
                .ThenInclude(s => s.ExternalReviews);
        }

        if (includeFlags.HasFlag(SeriesIncludes.ExternalRatings))
        {
            query = query
                .Include(s => s.ExternalSeriesMetadata)
                .ThenInclude(s => s.ExternalRatings);
        }

        if (includeFlags.HasFlag(SeriesIncludes.ExternalMetadata))
        {
            query = query
                .Include(s => s.ExternalSeriesMetadata);
        }

        if (includeFlags.HasFlag(SeriesIncludes.ExternalRecommendations))
        {
            query = query
                .Include(s => s.ExternalSeriesMetadata)
                .ThenInclude(s => s.ExternalRecommendations);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Metadata))
        {
            query = query
                .Include(s => s.Metadata)
                .ThenInclude(m => m.Genres.OrderBy(g => g.NormalizedTitle))
                .Include(s => s.Metadata)
                .ThenInclude(m => m.People)
                .ThenInclude(smp => smp.Person)
                .Include(s => s.Metadata)
                .ThenInclude(m => m.Tags.OrderBy(g => g.NormalizedTitle));
        }

        return query.AsSplitQuery();
    }

    public static IQueryable<AppUser> Includes(this IQueryable<AppUser> query, AppUserIncludes includeFlags)
    {
        if (includeFlags.HasFlag(AppUserIncludes.Bookmarks))
        {
            query = query.Include(u => u.Bookmarks);
        }

        if (includeFlags.HasFlag(AppUserIncludes.Progress))
        {
            query = query.Include(u => u.Progresses);
        }

        if (includeFlags.HasFlag(AppUserIncludes.ReadingLists))
        {
            query = query.Include(u => u.ReadingLists);
        }

        if (includeFlags.HasFlag(AppUserIncludes.ReadingListsWithItems))
        {
            query = query.Include(u => u.ReadingLists)
                .ThenInclude(r => r.Items);
        }

        if (includeFlags.HasFlag(AppUserIncludes.Ratings))
        {
            query = query.Include(u => u.Ratings);
        }

        if (includeFlags.HasFlag(AppUserIncludes.UserPreferences))
        {
            query = query
                .Include(u => u.UserPreferences)
                .ThenInclude(p => p.Theme);
        }

        if (includeFlags.HasFlag(AppUserIncludes.WantToRead))
        {
            query = query.Include(u => u.WantToRead);
        }

        if (includeFlags.HasFlag(AppUserIncludes.Devices))
        {
            query = query.Include(u => u.Devices);
        }

        if (includeFlags.HasFlag(AppUserIncludes.ScrobbleHolds))
        {
            query = query.Include(u => u.ScrobbleHolds);
        }

        if (includeFlags.HasFlag(AppUserIncludes.SmartFilters))
        {
            query = query.Include(u => u.SmartFilters);
        }

        if (includeFlags.HasFlag(AppUserIncludes.DashboardStreams))
        {
            query = query.Include(u => u.DashboardStreams)
                .ThenInclude(s => s.SmartFilter);
        }

        if (includeFlags.HasFlag(AppUserIncludes.SideNavStreams))
        {
            query = query.Include(u => u.SideNavStreams)
                .ThenInclude(s => s.SmartFilter);
        }

        if (includeFlags.HasFlag(AppUserIncludes.ExternalSources))
        {
            query = query.Include(u => u.ExternalSources);
        }

        if (includeFlags.HasFlag(AppUserIncludes.Collections))
        {
            query = query.Include(u => u.Collections)
                .ThenInclude(c => c.Items);
        }

        return query.AsSplitQuery();
    }

    public static IQueryable<ReadingList> Includes(this IQueryable<ReadingList> queryable,
        ReadingListIncludes includes)
    {
        if (includes.HasFlag(ReadingListIncludes.Items))
        {
            queryable = queryable.Include(r => r.Items.OrderBy(item => item.Order));
        }

        if (includes.HasFlag(ReadingListIncludes.ItemChapter))
        {
            queryable = queryable
                .Include(r => r.Items.OrderBy(item => item.Order))
                .ThenInclude(ri => ri.Chapter);
        }

        return queryable.AsSplitQuery();
    }

    public static IQueryable<Library> Includes(this IQueryable<Library> query, LibraryIncludes includeFlags)
    {
        if (includeFlags.HasFlag(LibraryIncludes.Folders))
        {
            query = query.Include(l => l.Folders);
        }

        if (includeFlags.HasFlag(LibraryIncludes.FileTypes))
        {
            query = query.Include(l => l.LibraryFileTypes);
        }

        if (includeFlags.HasFlag(LibraryIncludes.Series))
        {
            query = query.Include(l => l.Series);
        }

        if (includeFlags.HasFlag(LibraryIncludes.AppUser))
        {
            query = query.Include(l => l.AppUsers);
        }

        if (includeFlags.HasFlag(LibraryIncludes.ExcludePatterns))
        {
            query = query.Include(l => l.LibraryExcludePatterns);
        }

        return query.AsSplitQuery();
    }
}
