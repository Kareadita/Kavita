using System.Linq;
using API.DTOs.Filtering;
using API.Entities;

namespace API.Extensions.QueryExtensions.Filtering;

public static class SeriesSort
{
    /// <summary>
    /// Applies the correct sort based on <see cref="SortOptions"/>
    /// </summary>
    /// <param name="query"></param>
    /// <param name="sortOptions"></param>
    /// <returns></returns>
    public static IQueryable<Series> Sort(this IQueryable<Series> query, SortOptions? sortOptions)
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
                SortField.SortName => query.OrderBy(s => s.SortName.ToLower()),
                SortField.CreatedDate => query.OrderBy(s => s.Created),
                SortField.LastModifiedDate => query.OrderBy(s => s.LastModified),
                SortField.LastChapterAdded => query.OrderBy(s => s.LastChapterAdded),
                SortField.TimeToRead => query.OrderBy(s => s.AvgHoursToRead),
                SortField.ReleaseYear => query.OrderBy(s => s.Metadata.ReleaseYear),
                //SortField.ReadProgress => query.OrderBy()
                _ => query
            };
        }
        else
        {
            query = sortOptions.SortField switch
            {
                SortField.SortName => query.OrderByDescending(s => s.SortName.ToLower()),
                SortField.CreatedDate => query.OrderByDescending(s => s.Created),
                SortField.LastModifiedDate => query.OrderByDescending(s => s.LastModified),
                SortField.LastChapterAdded => query.OrderByDescending(s => s.LastChapterAdded),
                SortField.TimeToRead => query.OrderByDescending(s => s.AvgHoursToRead),
                SortField.ReleaseYear => query.OrderByDescending(s => s.Metadata.ReleaseYear),
                _ => query
            };
        }

        return query;
    }
}
