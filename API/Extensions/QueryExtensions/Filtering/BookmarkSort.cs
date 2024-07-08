using System.Linq;
using API.DTOs.Filtering;
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions.Filtering;
#nullable enable

public class BookmarkSeriesPair
{
    public AppUserBookmark Bookmark { get; init; } = null!;
    public Series Series { get; init; } = null!;
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

        query = sortOptions.SortField switch
        {
            SortField.SortName => query.DoOrderBy(s => s.Series.SortName.ToLower(), sortOptions),
            SortField.CreatedDate => query.DoOrderBy(s => s.Series.Created, sortOptions),
            SortField.LastModifiedDate => query.DoOrderBy(s => s.Series.LastModified, sortOptions),
            SortField.LastChapterAdded => query.DoOrderBy(s => s.Series.LastChapterAdded, sortOptions),
            SortField.TimeToRead => query.DoOrderBy(s => s.Series.AvgHoursToRead, sortOptions),
            SortField.ReleaseYear => query.DoOrderBy(s => s.Series.Metadata.ReleaseYear, sortOptions),
            SortField.ReadProgress => query.DoOrderBy(s => s.Series.Progress.Where(p => p.SeriesId == s.Series.Id).Select(p => p.LastModified).Max(), sortOptions),
            SortField.AverageRating => query.DoOrderBy(s => s.Series.ExternalSeriesMetadata.ExternalRatings
                .Where(p => p.SeriesId == s.Series.Id).Average(p => p.AverageScore), sortOptions),
            SortField.Random => query.DoOrderBy(s => EF.Functions.Random(), sortOptions),
            _ => query
        };

        return query;
    }
}
