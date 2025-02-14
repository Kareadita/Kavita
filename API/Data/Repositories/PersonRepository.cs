using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IPersonRepository
{
    void Attach(Person person);
    void Attach(IEnumerable<Person> person);
    void Remove(Person person);
    void Remove(ChapterPeople person);
    void Remove(SeriesMetadataPeople person);
    void Update(Person person);

    Task<IList<Person>> GetAllPeople();
    Task<IList<PersonDto>> GetAllPersonDtosAsync(int userId);
    Task<IList<PersonDto>> GetAllPersonDtosByRoleAsync(int userId, PersonRole role);
    Task RemoveAllPeopleNoLongerAssociated();
    Task<IList<PersonDto>> GetAllPeopleDtosForLibrariesAsync(int userId, List<int>? libraryIds = null);
    Task<int> GetCountAsync();

    Task<string> GetCoverImageAsync(int personId);
    Task<string?> GetCoverImageByNameAsync(string name);
    Task<IEnumerable<PersonRole>> GetRolesForPersonByName(int personId, int userId);
    Task<PagedList<BrowsePersonDto>> GetAllWritersAndSeriesCount(int userId, UserParams userParams);
    Task<Person?> GetPersonById(int personId);
    Task<PersonDto?> GetPersonDtoByName(string name, int userId);
    Task<Person> GetPersonByName(string name);
    Task<bool> IsNameUnique(string name);

    Task<IEnumerable<SeriesDto>> GetSeriesKnownFor(int personId);
    Task<IEnumerable<StandaloneChapterDto>> GetChaptersForPersonByRole(int personId, int userId, PersonRole role);
    Task<IList<Person>> GetPeopleByNames(List<string> normalizedNames);
    Task<Person?> GetPersonByAniListId(int aniListId);
}

public class PersonRepository : IPersonRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public PersonRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(Person person)
    {
        _context.Person.Attach(person);
    }

    public void Attach(IEnumerable<Person> person)
    {
        _context.Person.AttachRange(person);
    }

    public void Remove(Person person)
    {
        _context.Person.Remove(person);
    }

    public void Remove(ChapterPeople person)
    {
        _context.ChapterPeople.Remove(person);
    }

    public void Remove(SeriesMetadataPeople person)
    {
        _context.SeriesMetadataPeople.Remove(person);
    }

    public void Update(Person person)
    {
        _context.Person.Update(person);
    }

    public async Task RemoveAllPeopleNoLongerAssociated()
    {
        var peopleWithNoConnections = await _context.Person
            .Include(p => p.SeriesMetadataPeople)
            .Include(p => p.ChapterPeople)
            .Where(p => p.SeriesMetadataPeople.Count == 0 && p.ChapterPeople.Count == 0)
            .AsSplitQuery()
            .ToListAsync();

        _context.Person.RemoveRange(peopleWithNoConnections);

        await _context.SaveChangesAsync();
    }


    public async Task<IList<PersonDto>> GetAllPeopleDtosForLibrariesAsync(int userId, List<int>? libraryIds = null)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = await _context.Library.GetUserLibraries(userId).ToListAsync();

        if (libraryIds is {Count: > 0})
        {
            userLibs = userLibs.Where(libraryIds.Contains).ToList();
        }

        return await _context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(ageRating)
            .SelectMany(s => s.Metadata.People.Select(p => p.Person))
            .Distinct()
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .AsSplitQuery()
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.Person.CountAsync();
    }

    public async Task<string> GetCoverImageAsync(int personId)
    {
        return await _context.Person
            .Where(c => c.Id == personId)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync();
    }

    public async Task<string> GetCoverImageByNameAsync(string name)
    {
        var normalized = name.ToNormalized();
        return await _context.Person
            .Where(c => c.NormalizedName == normalized)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<PersonRole>> GetRolesForPersonByName(int personId, int userId)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        // Query roles from ChapterPeople
        var chapterRoles = await _context.Person
            .Where(p => p.Id == personId)
            .RestrictAgainstAgeRestriction(ageRating)
            .SelectMany(p => p.ChapterPeople.Select(cp => cp.Role))
            .Distinct()
            .ToListAsync();

        // Query roles from SeriesMetadataPeople
        var seriesRoles = await _context.Person
            .Where(p => p.Id == personId)
            .RestrictAgainstAgeRestriction(ageRating)
            .SelectMany(p => p.SeriesMetadataPeople.Select(smp => smp.Role))
            .Distinct()
            .ToListAsync();

        // Combine and return distinct roles
        return chapterRoles.Union(seriesRoles).Distinct();
    }

    public async Task<PagedList<BrowsePersonDto>> GetAllWritersAndSeriesCount(int userId, UserParams userParams)
    {
        List<PersonRole> roles = [PersonRole.Writer, PersonRole.CoverArtist];
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        var query = _context.Person
            .Where(p => p.SeriesMetadataPeople.Any(smp => roles.Contains(smp.Role)) || p.ChapterPeople.Any(cmp => roles.Contains(cmp.Role)))
            .RestrictAgainstAgeRestriction(ageRating)
            .Select(p => new BrowsePersonDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                CoverImage = p.CoverImage,
                SeriesCount = p.SeriesMetadataPeople
                    .Where(smp => roles.Contains(smp.Role))
                    .Select(smp => smp.SeriesMetadata.SeriesId)
                    .Distinct()
                    .Count(),
                IssueCount = p.ChapterPeople
                    .Where(cp => roles.Contains(cp.Role))
                    .Select(cp => cp.Chapter.Id)
                    .Distinct()
                    .Count()
            })
            .OrderBy(p => p.Name);

        return await PagedList<BrowsePersonDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<Person?> GetPersonById(int personId)
    {
        return await _context.Person.Where(p => p.Id == personId)
            .FirstOrDefaultAsync();
    }

    public async Task<PersonDto> GetPersonDtoByName(string name, int userId)
    {
        var normalized = name.ToNormalized();
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.Person
            .Where(p => p.NormalizedName == normalized)
            .RestrictAgainstAgeRestriction(ageRating)
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<Person> GetPersonByName(string name)
    {
        return await _context.Person.FirstOrDefaultAsync(p => p.NormalizedName == name.ToNormalized());
    }

    public async Task<bool> IsNameUnique(string name)
    {
        return !(await _context.Person.AnyAsync(p => p.Name == name));
    }

    public async Task<IEnumerable<SeriesDto>> GetSeriesKnownFor(int personId)
    {
        List<PersonRole> notValidRoles = [PersonRole.Location, PersonRole.Team, PersonRole.Other, PersonRole.Publisher, PersonRole.Translator];
        return await _context.Person
            .Where(p => p.Id == personId)
            .SelectMany(p => p.SeriesMetadataPeople.Where(smp => !notValidRoles.Contains(smp.Role)))
            .Select(smp => smp.SeriesMetadata)
            .Select(sm => sm.Series)
            .Distinct()
            .OrderByDescending(s => s.ExternalSeriesMetadata.AverageExternalRating)
            .Take(20)
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<StandaloneChapterDto>> GetChaptersForPersonByRole(int personId, int userId, PersonRole role)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.ChapterPeople
            .Where(cp => cp.PersonId == personId && cp.Role == role)
            .Select(cp => cp.Chapter)
            .RestrictAgainstAgeRestriction(ageRating)
            .OrderBy(ch => ch.SortOrder)
            .Take(20)
            .ProjectTo<StandaloneChapterDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IList<Person>> GetPeopleByNames(List<string> normalizedNames)
    {
        return await _context.Person
            .Where(p => normalizedNames.Contains(p.NormalizedName))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Person?> GetPersonByAniListId(int aniListId)
    {
        return await _context.Person
            .Where(p => p.AniListId == aniListId)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<Person>> GetAllPeople()
    {
        return await _context.Person
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IList<PersonDto>> GetAllPersonDtosAsync(int userId)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.Person
            .OrderBy(p => p.Name)
            .RestrictAgainstAgeRestriction(ageRating)
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IList<PersonDto>> GetAllPersonDtosByRoleAsync(int userId, PersonRole role)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.Person
            .Where(p => p.SeriesMetadataPeople.Any(smp => smp.Role == role) || p.ChapterPeople.Any(cp => cp.Role == role)) // Filter by role in both series and chapters
            .OrderBy(p => p.Name)
            .RestrictAgainstAgeRestriction(ageRating)
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}
