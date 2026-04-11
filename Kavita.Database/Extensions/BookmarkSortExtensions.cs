using System.Linq;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Filtering.v2.SortFields;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Extensions;

public static class BookmarkSortExtensions
{
    /// <summary>
    /// Applies the correct sort based on <see cref="SeriesSortOptionDto"/>
    /// </summary>
    /// <param name="query"></param>
    /// <param name="sortOptions"></param>
    /// <returns></returns>
    public static IQueryable<BookmarkSeriesPair> Sort(this IQueryable<BookmarkSeriesPair> query, SeriesSortOptionDto? sortOptions)
    {
        // If no sort options, default to using SortName
        sortOptions ??= new SeriesSortOptionDto()
        {
            IsAscending = true,
            SortField = SeriesSortField.SortName
        };

        query = sortOptions.SortField switch
        {
            SeriesSortField.SortName => query.DoOrderBy(s => s.Series.SortName.ToLower(), sortOptions),
            SeriesSortField.CreatedDate => query.DoOrderBy(s => s.Series.Created, sortOptions),
            SeriesSortField.LastModifiedDate => query.DoOrderBy(s => s.Series.LastModified, sortOptions),
            SeriesSortField.LastChapterAdded => query.DoOrderBy(s => s.Series.LastChapterAdded, sortOptions),
            SeriesSortField.TimeToRead => query.DoOrderBy(s => s.Series.AvgHoursToRead, sortOptions),
            SeriesSortField.ReleaseYear => query.DoOrderBy(s => s.Series.Metadata.ReleaseYear, sortOptions),
            SeriesSortField.ReadProgress => query.DoOrderBy(s => s.Series.Progress.Where(p => p.SeriesId == s.Series.Id).Select(p => p.LastModified).Max(), sortOptions),
            SeriesSortField.AverageRating => query.DoOrderBy(s => s.Series.ExternalSeriesMetadata.ExternalRatings
                .Where(p => p.SeriesId == s.Series.Id).Average(p => p.AverageScore), sortOptions),
            SeriesSortField.Random => query.DoOrderBy(s => EF.Functions.Random(), sortOptions),
            _ => query
        };

        return query;
    }
}
