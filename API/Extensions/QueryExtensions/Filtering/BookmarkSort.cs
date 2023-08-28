using System.Linq;
using API.DTOs.Filtering;
using API.Entities;

namespace API.Extensions.QueryExtensions.Filtering;

public class BookmarkSeriesPair
{
    public AppUserBookmark bookmark { get; set; }
    public Series series { get; set; }
}

public static class BookmarkSort
{
    /// <summary>
    /// Applies the correct sort based on <see cref="SortOptions"/>
    /// </summary>
    /// <param name="query"></param>
    /// <param name="sortOptions"></param>
    /// <returns></returns>
    public static IQueryable<BookmarkSeriesPair> Sort(this IQueryable<BookmarkSeriesPair> query, SortOptions? sortOptions)
    {
        // If no sort options, default to using SortName
        sortOptions ??= new SortOptions()
        {
            IsAscending = true,
            SortField = SortField.SortName
        };

        if (sortOptions.IsAscending)
        {
            query = sortOptions.SortField switch
            {
                SortField.SortName => query.OrderBy(s => s.series.SortName.ToLower()),
                SortField.CreatedDate => query.OrderBy(s => s.series.Created),
                SortField.LastModifiedDate => query.OrderBy(s => s.series.LastModified),
                SortField.LastChapterAdded => query.OrderBy(s => s.series.LastChapterAdded),
                SortField.TimeToRead => query.OrderBy(s => s.series.AvgHoursToRead),
                SortField.ReleaseYear => query.OrderBy(s => s.series.Metadata.ReleaseYear),
                _ => query
            };
        }
        else
        {
            query = sortOptions.SortField switch
            {
                SortField.SortName => query.OrderByDescending(s => s.series.SortName.ToLower()),
                SortField.CreatedDate => query.OrderByDescending(s => s.series.Created),
                SortField.LastModifiedDate => query.OrderByDescending(s => s.series.LastModified),
                SortField.LastChapterAdded => query.OrderByDescending(s => s.series.LastChapterAdded),
                SortField.TimeToRead => query.OrderByDescending(s => s.series.AvgHoursToRead),
                SortField.ReleaseYear => query.OrderByDescending(s => s.series.Metadata.ReleaseYear),
                _ => query
            };
        }

        return query;
    }
}
