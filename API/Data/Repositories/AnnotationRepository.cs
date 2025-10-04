using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.DTOs;
using API.DTOs.Filtering.v2;
using API.DTOs.Metadata.Browse.Requests;
using API.DTOs.Annotations;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions.QueryExtensions;
using API.Extensions.QueryExtensions.Filtering;
using API.Helpers;
using API.Helpers.Converters;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

public interface IAnnotationRepository
{
    void Attach(AppUserAnnotation annotation);
    void Update(AppUserAnnotation annotation);
    void Remove(AppUserAnnotation annotation);
    void Remove(IEnumerable<AppUserAnnotation> annotations);
    Task<AnnotationDto?> GetAnnotationDto(int id);
    Task<AppUserAnnotation?> GetAnnotation(int id);
    Task<IList<AppUserAnnotation>> GetAllAnnotations();
    Task<IList<AppUserAnnotation>> GetAnnotations(int userId, IList<int> ids);
    Task<IList<FullAnnotationDto>> GetFullAnnotationsByUserIdAsync(int userId);
    Task<IList<FullAnnotationDto>> GetFullAnnotations(int userId, IList<int> annotationIds);
    Task<PagedList<AnnotationDto>> GetAnnotationDtos(int userId, BrowseAnnotationFilterDto filter, UserParams userParams);
    Task<List<SeriesDto>> GetSeriesWithAnnotations(int userId);
}

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

    public async Task<AnnotationDto?> GetAnnotationDto(int id)
    {
        return await context.AppUserAnnotation
            .ProjectTo<AnnotationDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AppUserAnnotation?> GetAnnotation(int id)
    {
        return await context.AppUserAnnotation
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IList<AppUserAnnotation>> GetAllAnnotations()
    {
        return await context.AppUserAnnotation.ToListAsync();
    }

    public async Task<IList<AppUserAnnotation>> GetAnnotations(int userId, IList<int> ids)
    {
        var userPreferences = await context.AppUserPreferences.ToListAsync();

        return await context.AppUserAnnotation
            .Where(a => ids.Contains(a.Id))
            .RestrictBySocialPreferences(userId, userPreferences)
            .ToListAsync();
    }

    public async Task<PagedList<AnnotationDto>> GetAnnotationDtos(int userId, BrowseAnnotationFilterDto filter, UserParams userParams)
    {
        var query = await CreatedFilteredAnnotationQueryable(userId, filter);
        return await PagedList<AnnotationDto>.CreateAsync(query, userParams);
    }

    public async Task<List<SeriesDto>> GetSeriesWithAnnotations(int userId)
    {
        var userPreferences = await context.AppUserPreferences.ToListAsync();

        var libraryIds = context.AppUser.GetLibraryIdsForUser(userId);
        var userRating = await context.AppUser.GetUserAgeRestriction(userId);

        var seriesIdsWithAnnotations = await context.AppUserAnnotation
            .RestrictBySocialPreferences(userId, userPreferences)
            .Select(a => a.SeriesId)
            .ToListAsync();

        return await context.Series
            .Where(s => libraryIds.Contains(s.LibraryId) && seriesIdsWithAnnotations.Contains(s.Id))
            .RestrictAgainstAgeRestriction(userRating)
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .ToListAsync();

    }

    private async Task<IQueryable<AnnotationDto>> CreatedFilteredAnnotationQueryable(int userId, BrowseAnnotationFilterDto filter)
    {
        var allLibrariesCount = await context.Library.CountAsync();
        var userLibs = await context.Library.GetUserLibraries(userId).ToListAsync();
        var seriesIds = await context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .Select(s => s.Id)
            .ToListAsync();

        var userPreferences = await context.AppUserPreferences.ToListAsync();

        var query = context.AppUserAnnotation.AsNoTracking();

        query = BuildAnnotationFilterQuery(userId, filter, query);

        query = query
            .WhereIf(allLibrariesCount != userLibs.Count, a => seriesIds.Contains(a.SeriesId))
            .RestrictBySocialPreferences(userId, userPreferences);

        var sortedQuery = query.SortBy(filter.SortOptions);
        var limitedQuery = filter.LimitTo <= 0 ? sortedQuery : sortedQuery.Take(filter.LimitTo);

        return limitedQuery.ProjectTo<AnnotationDto>(mapper.ConfigurationProvider);
    }

    private static IQueryable<AppUserAnnotation> BuildAnnotationFilterQuery(int userId, BrowseAnnotationFilterDto filter, IQueryable<AppUserAnnotation> query)
    {
        if (filter.Statements == null || filter.Statements.Count == 0) return query;

        // Manual intervention for Highlight slots, as they are not user recognisable. But would make sense
        // to miss match between users
        if (filter.Statements.Any(s => s.Field == AnnotationFilterField.HighlightSlot))
        {
            filter.Statements.Add(new AnnotationFilterStatementDto
            {
                Field = AnnotationFilterField.Owner,
                Comparison = FilterComparison.Equal,
                Value = $"{userId}",
            });
        }

        var queries = filter.Statements
            .Select(statement => BuildAnnotationFilterGroup(statement, query))
            .ToList();

        return filter.Combination == FilterCombination.And
            ? queries.Aggregate((q1, q2) => q1.Intersect(q2))
            : queries.Aggregate((q1, q2) => q1.Union(q2));
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
            _ => throw new ArgumentOutOfRangeException(nameof(statement.Field), $"Unexpected value for field: {statement.Field}")
        };
    }

    public async Task<IList<FullAnnotationDto>> GetFullAnnotations(int userId, IList<int> annotationIds)
    {
        var userPreferences = await context.AppUserPreferences.ToListAsync();

        return await context.AppUserAnnotation
            .AsNoTracking()
            .Where(a => annotationIds.Contains(a.Id))
            .RestrictBySocialPreferences(userId, userPreferences)
            .ProjectTo<FullAnnotationDto>(mapper.ConfigurationProvider)
            .OrderFullAnnotation()
            .ToListAsync();
    }

    /// <summary>
    /// This does not track!
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IList<FullAnnotationDto>> GetFullAnnotationsByUserIdAsync(int userId)
    {
        var userPreferences = await context.AppUserPreferences.ToListAsync();

        return await context.AppUserAnnotation
            .RestrictBySocialPreferences(userId, userPreferences)
            .ProjectTo<FullAnnotationDto>(mapper.ConfigurationProvider)
            .OrderFullAnnotation()
            .ToListAsync();
    }
}
