using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Helpers;
using Kavita.Database.Converters;
using Kavita.Database.Extensions;
using Kavita.Database.Extensions.Filters;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Annotations;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.FilterFields;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;


public class AnnotationRepository(DataContext context, IMapper mapper) : IAnnotationRepository
{
    public void Attach(AppUserAnnotation annotation)
    {
        context.AppUserAnnotation.Attach(annotation);
    }

    public void Update(AppUserAnnotation annotation)
    {
        context.AppUserAnnotation.Entry(annotation).State = EntityState.Modified;
    }

    public void Remove(AppUserAnnotation annotation)
    {
        context.AppUserAnnotation.Remove(annotation);
    }

    public void Remove(IEnumerable<AppUserAnnotation> annotations)
    {
        context.AppUserAnnotation.RemoveRange(annotations);
    }

    public async Task<AnnotationDto?> GetAnnotationDto(int id, CancellationToken ct = default)
    {
        return await context.AppUserAnnotation
            .ProjectTo<AnnotationDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<AppUserAnnotation?> GetAnnotation(int id, CancellationToken ct = default)
    {
        return await context.AppUserAnnotation
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IList<AppUserAnnotation>> GetAllAnnotations(CancellationToken ct = default)
    {
        return await context.AppUserAnnotation.ToListAsync(ct);
    }

    public async Task<IList<AppUserAnnotation>> GetAnnotations(int userId, IList<int> ids, CancellationToken ct = default)
    {
        var userPreferences = await context.AppUserPreferences.ToListAsync(ct);

        return await context.AppUserAnnotation
            .Where(a => ids.Contains(a.Id))
            .RestrictBySocialPreferences(userId, userPreferences)
            .ToListAsync(ct);
    }

    public async Task<PagedList<AnnotationDto>> GetAnnotationDtos(int userId, AnnotationFilterDto filter,
        UserParams userParams, CancellationToken ct = default)
    {
        var query = await CreatedFilteredAnnotationQueryable(userId, filter);
        return await PagedList<AnnotationDto>.CreateAsync(query, userParams, ct);
    }

    public async Task<List<SeriesDto>> GetSeriesWithAnnotations(int userId, CancellationToken ct = default)
    {
        var userPreferences = await context.AppUserPreferences.ToListAsync(ct);

        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        var seriesIdsWithAnnotations = await context.AppUserAnnotation
            .RestrictBySocialPreferences(userId, userPreferences)
            .Select(a => a.SeriesId)
            .ToListAsync(ct);

        return await context.Series
            .Where(s => libraryIds.Contains(s.LibraryId) && seriesIdsWithAnnotations.Contains(s.Id))
            .RestrictAgainstAgeRestriction(userRating)
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

    }

    private async Task<IQueryable<AnnotationDto>> CreatedFilteredAnnotationQueryable(int userId, AnnotationFilterDto filter)
    {
        var allLibrariesCount = await context.Library.CountAsync();
        var userLibs = await context.Library.GetUserLibraries(userId).ToListAsync();
        var seriesIds = await context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .Select(s => s.Id)
            .ToListAsync();

        var userPreferences = await context.AppUserPreferences.ToListAsync();

        var query = context.AppUserAnnotation.AsNoTracking();

        query = FilterQueryBuilder.Apply(filter, query,
            BuildAnnotationFilterGroup,
            preProcess: f =>
            {
                // Manual intervention for Highlight slots, as they are not user recognizable.
                // But would make sense to miss match between users
                if (f.Statements.Any(s => s.Field == AnnotationFilterField.HighlightSlot))
                {
                    f.Statements.Add(new AnnotationFilterStatementDto
                    {
                        Field = AnnotationFilterField.Owner,
                        Comparison = FilterComparison.Equal,
                        Value = $"{userId}",
                    });
                }
            });

        query = query
            .WhereIf(allLibrariesCount != userLibs.Count, a => seriesIds.Contains(a.SeriesId))
            .RestrictBySocialPreferences(userId, userPreferences);

        var sortedQuery = query.SortBy(filter.SortOptions);
        var limitedQuery = sortedQuery.ApplyLimit(filter.LimitTo);

        return limitedQuery.ProjectTo<AnnotationDto>(mapper.ConfigurationProvider);
    }

    private static IQueryable<AppUserAnnotation> BuildAnnotationFilterGroup(AnnotationFilterStatementDto statement, IQueryable<AppUserAnnotation> query)
    {
        var value = AnnotationFilterFieldValueConverter.ConvertValue(statement.Field, statement.Value);

        return statement.Field switch
        {
            AnnotationFilterField.Owner => query.IsOwnedBy(true, statement.Comparison, (IList<int>) value),
            AnnotationFilterField.Library => query.IsInLibrary(true, statement.Comparison, (IList<int>) value),
            AnnotationFilterField.Series => query.HasSeries(true, statement.Comparison, (IList<int>) value),
            AnnotationFilterField.HighlightSlot => query.IsUsingHighlights(true, statement.Comparison, (IList<int>) value),
            AnnotationFilterField.Spoiler => query.Where(a => !(bool) value || !a.ContainsSpoiler),
            AnnotationFilterField.Comment => query.HasCommented(true, statement.Comparison, (string) value),
            AnnotationFilterField.Selection => query.HasSelected(true, statement.Comparison, (string) value),
            AnnotationFilterField.Likes => query.HasLikes(true, statement.Comparison, (int) value),
            AnnotationFilterField.LikedBy => query.IsLikedBy(true, statement.Comparison, (IList<int>) value),
            _ => throw new ArgumentOutOfRangeException(nameof(statement.Field), $"Unexpected value for field: {statement.Field}")
        };
    }

    public async Task<IList<FullAnnotationDto>> GetFullAnnotations(int userId, IList<int> annotationIds,
        CancellationToken ct = default)
    {
        var userPreferences = await context.AppUserPreferences.ToListAsync(ct);

        return await context.AppUserAnnotation
            .AsNoTracking()
            .Where(a => annotationIds.Contains(a.Id))
            .RestrictBySocialPreferences(userId, userPreferences)
            .ProjectTo<FullAnnotationDto>(mapper.ConfigurationProvider)
            .OrderFullAnnotation()
            .ToListAsync(ct);
    }

    /// <summary>
    /// This does not track!
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<FullAnnotationDto>> GetFullAnnotationsByUserIdAsync(int userId, CancellationToken ct = default)
    {
        var userPreferences = await context.AppUserPreferences.ToListAsync(ct);

        return await context.AppUserAnnotation
            .RestrictBySocialPreferences(userId, userPreferences)
            .ProjectTo<FullAnnotationDto>(mapper.ConfigurationProvider)
            .OrderFullAnnotation()
            .ToListAsync(ct);
    }
}
