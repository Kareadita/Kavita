using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Database.Converters;
using Kavita.Database.Extensions;
using Kavita.Database.Extensions.Filters;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Metadata.Browse;
using Kavita.Models.DTOs.Metadata.Browse.Requests;
using Kavita.Models.DTOs.Person;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Person;
using Kavita.Models.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class PersonRepository(DataContext context, IMapper mapper) : IPersonRepository
{
    public void Attach(Person person)
    {
        context.Person.Attach(person);
    }

    public void Attach(IEnumerable<Person> person)
    {
        context.Person.AttachRange(person);
    }

    public void Remove(Person person)
    {
        context.Person.Remove(person);
    }

    public void Remove(ChapterPeople person)
    {
        context.ChapterPeople.Remove(person);
    }

    public void Remove(SeriesMetadataPeople person)
    {
        context.SeriesMetadataPeople.Remove(person);
    }

    public void Update(Person person)
    {
        context.Person.Update(person);
    }

    public async Task RemoveAllPeopleNoLongerAssociated(CancellationToken ct = default)
    {
        var peopleWithNoConnections = await context.Person
            .Include(p => p.SeriesMetadataPeople)
            .Include(p => p.ChapterPeople)
            .Where(p => p.SeriesMetadataPeople.Count == 0 && p.ChapterPeople.Count == 0)
            .AsSplitQuery()
            .ToListAsync(ct);

        context.Person.RemoveRange(peopleWithNoConnections);

        await context.SaveChangesAsync(ct);
    }


    public async Task<IList<PersonDto>> GetAllPeopleDtosForLibrariesAsync(int userId, List<int>? libraryIds = null,
        PersonIncludes includes = PersonIncludes.None, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = await context.Library.GetUserLibraries(userId).ToListAsync(ct);

        if (libraryIds is {Count: > 0})
        {
            userLibs = userLibs.Where(libraryIds.Contains).ToList();
        }

        return await context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(ageRating)
            .SelectMany(s => s.Metadata.People.Select(p => p.Person))
            .Includes(includes)
            .Distinct()
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .AsSplitQuery()
            .ProjectTo<PersonDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }


    public async Task<string?> GetCoverImageAsync(int personId, CancellationToken ct = default)
    {
        return await context.Person
            .Where(c => c.Id == personId)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IList<string?>> GetAllCoverImagesAsync(CancellationToken ct = default)
    {
        return await context.Person
            .Select(p => p.CoverImage)
            .ToListAsync(ct);
    }

    public async Task<string?> GetCoverImageByNameAsync(string name, CancellationToken ct = default)
    {
        var normalized = name.ToNormalized();
        return await context.Person
            .Where(c => c.NormalizedName == normalized)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<PersonRole>> GetRolesForPersonByName(int personId, int userId,
        CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = context.Library.GetUserLibraries(userId);

        // Query roles from ChapterPeople
        var chapterRoles = await context.Person
            .Where(p => p.Id == personId)
            .SelectMany(p => p.ChapterPeople)
            .RestrictAgainstAgeRestriction(ageRating)
            .RestrictByLibrary(userLibs)
            .Select(cp => cp.Role)
            .Distinct()
            .ToListAsync(ct);

        // Query roles from SeriesMetadataPeople
        var seriesRoles = await context.Person
            .Where(p => p.Id == personId)
            .SelectMany(p => p.SeriesMetadataPeople)
            .RestrictAgainstAgeRestriction(ageRating)
            .RestrictByLibrary(userLibs)
            .Select(smp => smp.Role)
            .Distinct()
            .ToListAsync(ct);

        // Combine and return distinct roles
        return chapterRoles.Union(seriesRoles).Distinct();
    }

    public async Task<PagedList<BrowsePersonDto>> GetBrowsePersonDtos(int userId, BrowsePersonFilterDto filter,
        UserParams userParams, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);

        var query = await CreateFilteredPersonQueryable(userId, filter, ageRating, ct);

        return await PagedList<BrowsePersonDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }

    private async Task<IQueryable<BrowsePersonDto>> CreateFilteredPersonQueryable(int userId, BrowsePersonFilterDto filter, AgeRestriction ageRating, CancellationToken ct = default)
    {
        var allLibrariesCount = await context.Library.CountAsync(ct);
        var userLibs = await context.Library.GetUserLibraries(userId).ToListAsync(ct);

        var seriesIds = await context.Series.Where(s => userLibs.Contains(s.LibraryId)).Select(s => s.Id).ToListAsync(ct);

        var query = context.Person.AsNoTracking();

        // Apply filtering based on statements
        query = BuildPersonFilterQuery(userId, filter, query);

        // Apply restrictions
        query = query.RestrictAgainstAgeRestriction(ageRating)
            .WhereIf(allLibrariesCount != userLibs.Count,
                person => person.ChapterPeople.Any(cp => seriesIds.Contains(cp.Chapter.Volume.SeriesId)) ||
                          person.SeriesMetadataPeople.Any(smp => seriesIds.Contains(smp.SeriesMetadata.SeriesId)));

        // Apply sorting and limiting
        var sortedQuery = query.SortBy(filter.SortOptions);

        var limitedQuery = ApplyPersonLimit(sortedQuery, filter.LimitTo);

        return limitedQuery.Select(p => new BrowsePersonDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            CoverImage = p.CoverImage,
            SeriesCount = p.SeriesMetadataPeople
                .Select(smp => smp.SeriesMetadata)
                .Where(sm => allLibrariesCount == userLibs.Count || seriesIds.Contains(sm.SeriesId))
                .RestrictAgainstAgeRestriction(ageRating)
                .Distinct()
                .Count(),
            ChapterCount = p.ChapterPeople
                .Select(chp => chp.Chapter)
                .Where(ch => allLibrariesCount == userLibs.Count || seriesIds.Contains(ch.Volume.SeriesId))
                .RestrictAgainstAgeRestriction(ageRating)
                .Distinct()
                .Count(),
        });
    }

    private static IQueryable<Person> BuildPersonFilterQuery(int userId, BrowsePersonFilterDto filterDto, IQueryable<Person> query)
    {
        if (filterDto.Statements == null || filterDto.Statements.Count == 0) return query;

        var queries = filterDto.Statements
            .Select(statement => BuildPersonFilterGroup(userId, statement, query))
            .ToList();

        return filterDto.Combination == FilterCombination.And
            ? queries.Aggregate((q1, q2) => q1.Intersect(q2))
            : queries.Aggregate((q1, q2) => q1.Union(q2));
    }

    private static IQueryable<Person> BuildPersonFilterGroup(int userId, PersonFilterStatementDto statement, IQueryable<Person> query)
    {
        var value = PersonFilterFieldValueConverter.ConvertValue(statement.Field, statement.Value);

        return statement.Field switch
        {
            PersonFilterField.Name => query.HasPersonName(true, statement.Comparison, (string)value),
            PersonFilterField.Role => query.HasPersonRole(true, statement.Comparison, (IList<PersonRole>)value),
            PersonFilterField.SeriesCount => query.HasPersonSeriesCount(true, statement.Comparison, (int)value),
            PersonFilterField.ChapterCount => query.HasPersonChapterCount(true, statement.Comparison, (int)value),
            _ => throw new ArgumentOutOfRangeException(nameof(statement.Field), $"Unexpected value for field: {statement.Field}")
        };
    }

    private static IQueryable<Person> ApplyPersonLimit(IQueryable<Person> query, int limit)
    {
        return limit <= 0 ? query : query.Take(limit);
    }

    public async Task<Person?> GetPersonById(int personId, PersonIncludes includes = PersonIncludes.None,
        CancellationToken ct = default)
    {
        return await context.Person.Where(p => p.Id == personId)
            .Includes(includes)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PersonDto?> GetPersonDtoByName(string name, int userId,
        PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default)
    {
        var normalized = name.ToNormalized();
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = context.Library.GetUserLibraries(userId);

        return await context.Person
            .Where(p => p.NormalizedName == normalized)
            .Includes(includes)
            .RestrictAgainstAgeRestriction(ageRating)
            .RestrictByLibrary(userLibs)
            .ProjectTo<PersonDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Person?> GetPersonByNameOrAliasAsync(string name, PersonIncludes includes = PersonIncludes.Aliases,
        CancellationToken ct = default)
    {
        var normalized = name.ToNormalized();
        return context.Person
            .Includes(includes)
            .Where(p => p.NormalizedName == normalized || p.Aliases.Any(pa => pa.NormalizedAlias == normalized))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsNameUnique(string name, CancellationToken ct = default)
    {
        // Should this use Normalized to check?
        return !await context.Person
            .Includes(PersonIncludes.Aliases)
            .AnyAsync(p => p.Name == name || p.Aliases.Any(pa => pa.Alias == name), ct);
    }

    public async Task<IEnumerable<SeriesDto>> GetSeriesKnownFor(int personId, int userId, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = await context.Library.GetUserLibraries(userId).ToListAsync(ct);

        return await context.Person
            .Where(p => p.Id == personId)
            .SelectMany(p => p.SeriesMetadataPeople)
            .Select(smp => smp.SeriesMetadata)
            .Select(sm => sm.Series)
            .RestrictAgainstAgeRestriction(ageRating)
            .Where(s => userLibs.Contains(s.LibraryId))
            .Distinct()
            .OrderByDescending(s => s.ExternalSeriesMetadata.AverageExternalRating)
            .Take(20)
            .ProjectToWithProgress<Series, SeriesDto>(mapper.ConfigurationProvider, userId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<StandaloneChapterDto>> GetChaptersForPersonByRole(int personId, int userId,
        PersonRole role, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = context.Library.GetUserLibraries(userId);

        return await context.ChapterPeople
            .Where(cp => cp.PersonId == personId && cp.Role == role)
            .Select(cp => cp.Chapter)
            .RestrictAgainstAgeRestriction(ageRating)
            .RestrictByLibrary(userLibs)
            .OrderBy(ch => ch.Volume.MinNumber) // Group/Sort volumes as well
            .ThenBy(ch => ch.SortOrder)
            .Take(20)
            .ProjectToWithProgress<Chapter, StandaloneChapterDto>(mapper.ConfigurationProvider, userId)
            .ToListAsync(ct);
    }

    public async Task<IList<Person>> GetPeopleByNames(List<string> normalizedNames,
        PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default)
    {
        return await context.Person
            .Includes(includes)
            .Where(p => normalizedNames.Contains(p.NormalizedName) || p.Aliases.Any(pa => normalizedNames.Contains(pa.NormalizedAlias)))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Person?> GetPersonByAniListId(int aniListId, PersonIncludes includes = PersonIncludes.Aliases,
        CancellationToken ct = default)
    {
        return await context.Person
            .Where(p => p.AniListId == aniListId)
            .Includes(includes)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IList<PersonDto>> SearchPeople(string searchQuery,
        PersonIncludes includes = PersonIncludes.Aliases, CancellationToken ct = default)
    {
        searchQuery = searchQuery.ToNormalized();

        return await context.Person
            .Includes(includes)
            .Where(p => EF.Functions.Like(p.NormalizedName, $"%{searchQuery}%")
            || p.Aliases.Any(pa => EF.Functions.Like(pa.NormalizedAlias, $"%{searchQuery}%")))
            .ProjectTo<PersonDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }


    public async Task<bool> AnyAliasExist(string alias, CancellationToken ct = default)
    {
        var normalizedAlias = alias.ToNormalized();
        return await context.PersonAlias.AnyAsync(pa => pa.NormalizedAlias == normalizedAlias, ct);
    }


    public async Task<IList<Person>> GetAllPeople(PersonIncludes includes = PersonIncludes.Aliases,
        CancellationToken ct = default)
    {
        return await context.Person
            .Includes(includes)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<IList<PersonDto>> GetAllPersonDtosAsync(int userId, PersonIncludes includes = PersonIncludes.None,
        CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = context.Library.GetUserLibraries(userId);

        return await context.Person
            .Includes(includes)
            .RestrictAgainstAgeRestriction(ageRating)
            .RestrictByLibrary(userLibs)
            .OrderBy(p => p.Name)
            .ProjectTo<PersonDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<IList<PersonDto>> GetAllPersonDtosByRoleAsync(int userId, PersonRole role,
        PersonIncludes includes = PersonIncludes.None, CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = context.Library.GetUserLibraries(userId);

        return await context.Person
            .Where(p => p.SeriesMetadataPeople.Any(smp => smp.Role == role) || p.ChapterPeople.Any(cp => cp.Role == role)) // Filter by role in both series and chapters
            .Includes(includes)
            .RestrictAgainstAgeRestriction(ageRating)
            .RestrictByLibrary(userLibs)
            .OrderBy(p => p.Name)
            .ProjectTo<PersonDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }
}
