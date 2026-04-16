using System.Linq;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Filtering.v2.SortFields;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;
using Kavita.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Extensions;

public static class SeriesSortExtensions
{
    /// <summary>
    /// Applies the correct sort based on <see cref="SeriesSortOptionDto"/>
    /// </summary>
    /// <param name="query"></param>
    /// <param name="userId"></param>
    /// <param name="sortOptions"></param>
    /// <returns></returns>
    public static IQueryable<Series> Sort(this IQueryable<Series> query, int userId, SeriesSortOptionDto? sortOptions)
    {
        // If no sort options, default to using SortName
        sortOptions ??= new SeriesSortOptionDto()
        {
            IsAscending = true,
            SortField = SeriesSortField.SortName
        };

        query = sortOptions.SortField switch
        {
            SeriesSortField.SortName => query.DoOrderBy(s => s.SortName.ToLower(), sortOptions),
            SeriesSortField.CreatedDate => query.DoOrderBy(s => s.Created, sortOptions),
            SeriesSortField.LastModifiedDate => query.DoOrderBy(s => s.LastModified, sortOptions),
            SeriesSortField.LastChapterAdded => query.DoOrderBy(s => s.LastChapterAdded, sortOptions),
            SeriesSortField.TimeToRead => query.DoOrderBy(s => s.AvgHoursToRead, sortOptions),
            SeriesSortField.ReleaseYear => query.DoOrderBy(s => s.Metadata.ReleaseYear, sortOptions),
            SeriesSortField.ReadProgress => query.DoOrderBy(s => s.Progress.Where(p => p.SeriesId == s.Id && p.AppUserId == userId)
                .Select(p => p.LastModifiedUtc)
                .Max(), sortOptions),
            SeriesSortField.AverageRating => query.DoOrderBy(s => s.ExternalSeriesMetadata.ExternalRatings
                .Where(p => p.SeriesId == s.Id).Average(p => p.AverageScore), sortOptions),
            SeriesSortField.UserRating => query.DoOrderBy(s => s.Ratings.Where(r => r.SeriesId == s.Id && r.AppUserId == userId).Max(r => r.Rating), sortOptions)
                .ThenBy(s => s.SortName.ToLower()),
            SeriesSortField.Random => query.DoOrderBy(s => EF.Functions.Random(), sortOptions),
            SeriesSortField.UnreadChapterCount => query.DoOrderBy(s => s.Volumes.Sum(
                v => v.Chapters.Count(
                    c => c.UserProgress.Where(p => p.AppUserId == userId)
                        .Select(p => p.PagesRead).FirstOrDefault() == 0
                )), sortOptions),
            _ => query
        };

        return query;
    }
}
