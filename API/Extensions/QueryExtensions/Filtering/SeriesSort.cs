using System.Linq;
using API.DTOs.Filtering;
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions.Filtering;
#nullable enable

public static class SeriesSort
{
    /// <summary>
    /// Applies the correct sort based on <see cref="SortOptions"/>
    /// </summary>
    /// <param name="query"></param>
    /// <param name="sortOptions"></param>
    /// <returns></returns>
    public static IQueryable<Series> Sort(this IQueryable<Series> query, int userId, SortOptions? sortOptions)
    {
        // If no sort options, default to using SortName
        sortOptions ??= new SortOptions()
        {
            IsAscending = true,
            SortField = SortField.SortName
        };

        query = sortOptions.SortField switch
        {
            SortField.SortName => query.DoOrderBy(s => s.SortName.ToLower(), sortOptions),
            SortField.CreatedDate => query.DoOrderBy(s => s.Created, sortOptions),
            SortField.LastModifiedDate => query.DoOrderBy(s => s.LastModified, sortOptions),
            SortField.LastChapterAdded => query.DoOrderBy(s => s.LastChapterAdded, sortOptions),
            SortField.TimeToRead => query.DoOrderBy(s => s.AvgHoursToRead, sortOptions),
            SortField.ReleaseYear => query.DoOrderBy(s => s.Metadata.ReleaseYear, sortOptions),
            SortField.ReadProgress => query.DoOrderBy(s => s.Progress.Where(p => p.SeriesId == s.Id && p.AppUserId == userId)
                .Select(p => p.LastModified) // TODO: Migrate this to UTC
                .Max(), sortOptions),
            SortField.AverageRating => query.DoOrderBy(s => s.ExternalSeriesMetadata.ExternalRatings
                .Where(p => p.SeriesId == s.Id).Average(p => p.AverageScore), sortOptions),
            SortField.Random => query.DoOrderBy(s => EF.Functions.Random(), sortOptions),
            _ => query
        };

        return query;
    }
}
