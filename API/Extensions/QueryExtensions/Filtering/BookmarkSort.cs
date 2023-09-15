using System.Linq;
using API.DTOs.Filtering;
using API.Entities;

namespace API.Extensions.QueryExtensions.Filtering;

public class BookmarkSeriesPair
{
    public AppUserBookmark Bookmark { get; set; }
    public Series Series { get; set; }
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
                SortField.SortName => query.OrderBy(s => s.Series.SortName.ToLower()),
                SortField.CreatedDate => query.OrderBy(s => s.Series.Created),
                SortField.LastModifiedDate => query.OrderBy(s => s.Series.LastModified),
                SortField.LastChapterAdded => query.OrderBy(s => s.Series.LastChapterAdded),
                SortField.TimeToRead => query.OrderBy(s => s.Series.AvgHoursToRead),
                SortField.ReleaseYear => query.OrderBy(s => s.Series.Metadata.ReleaseYear),
                SortField.ReadProgress => query.OrderBy(s => s.Series.Progress.Where(p => p.SeriesId == s.Series.Id).Select(p => p.LastModified).Max()),
                _ => query
            };
        }
        else
        {
            query = sortOptions.SortField switch
            {
                SortField.SortName => query.OrderByDescending(s => s.Series.SortName.ToLower()),
                SortField.CreatedDate => query.OrderByDescending(s => s.Series.Created),
                SortField.LastModifiedDate => query.OrderByDescending(s => s.Series.LastModified),
                SortField.LastChapterAdded => query.OrderByDescending(s => s.Series.LastChapterAdded),
                SortField.TimeToRead => query.OrderByDescending(s => s.Series.AvgHoursToRead),
                SortField.ReleaseYear => query.OrderByDescending(s => s.Series.Metadata.ReleaseYear),
                SortField.ReadProgress => query.OrderByDescending(s => s.Series.Progress.Where(p => p.SeriesId == s.Series.Id).Select(p => p.LastModified).Max()),
                _ => query
            };
        }

        return query;
    }
}
