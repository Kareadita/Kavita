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
    void Remove(Person person);
    void Update(Person person);

    Task<IList<Person>> GetAllPeople();
    Task<IList<PersonDto>> GetAllPersonDtosAsync(int userId);
    Task<IList<PersonDto>> GetAllPersonDtosByRoleAsync(int userId, PersonRole role);
    Task RemoveAllPeopleNoLongerAssociated();
    Task<IList<PersonDto>> GetAllPeopleDtosForLibrariesAsync(int userId, List<int>? libraryIds = null);
    Task<int> GetCountAsync();

    //Task<IList<Person>> GetAllPeopleByRoleAndNames(PersonRole role, IEnumerable<string> normalizeNames);
    Task<string> GetCoverImageAsync(int personId);
    Task<string?> GetCoverImageByNameAsync(string name);
    Task<PersonDto> GetPersonDtoAsync(int personId, int userId);
    Task<IEnumerable<PersonRole>> GetRolesForPerson(int personId, int userId);
    Task<IEnumerable<PersonRole>> GetRolesForPersonByName(string name, int userId);
    Task<PagedList<BrowsePersonDto>> GetAllWritersAndSeriesCount(int userId, UserParams userParams);
    Task<Person?> GetPersonById(int personId);
    Task<PersonDto?> GetPersonDtoByName(string name, int userId);

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

    public void Remove(Person person)
    {
        _context.Person.Remove(person);
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

    // public async Task<IList<Person>> GetAllPeopleByRoleAndNames(PersonRole role, IEnumerable<string> normalizeNames)
    // {
    //     return await _context.Person
    //         .Where(p => p.Role == role && normalizeNames.Contains(p.NormalizedName))
    //         .ToListAsync();
    // }

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

    public async Task<PersonDto> GetPersonDtoAsync(int personId, int userId)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.Person
            .Where(p => p.Id == personId)
            .RestrictAgainstAgeRestriction(ageRating)
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PersonRole>> GetRolesForPerson(int personId, int userId)
    {
        // TODO: This will need to check both series and chapters (in cases where komf only updates series)
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.Person
            .Where(p => p.Id == personId)
            .RestrictAgainstAgeRestriction(ageRating)
            .SelectMany(p => p.ChapterPeople.Select(cp => cp.Role))
            .Distinct()
            .ToListAsync();
    }

    public async Task<IEnumerable<PersonRole>> GetRolesForPersonByName(string name, int userId)
    {
        // TODO: This will need to check both series and chapters (in cases where komf only updates series)
        var normalized = name.ToNormalized();
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        return await _context.Person
            .Where(p => p.NormalizedName == normalized)
            .RestrictAgainstAgeRestriction(ageRating)
            .SelectMany(p => p.ChapterPeople.Select(cp => cp.Role))
            .Distinct()
            .ToListAsync();
    }

    public async Task<PagedList<BrowsePersonDto>> GetAllWritersAndSeriesCount(int userId, UserParams userParams)
    {
        // TODO: Implement People Support
        // var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);
        //
        // var query = _context.Person
        //     .Where(p => p.Role == PersonRole.Writer)
        //     .RestrictAgainstAgeRestriction(ageRating)
        //     .Select(p => new BrowsePersonDto
        //     {
        //         Id = p.Id,
        //         Name = p.Name,
        //         Role = p.Role,
        //         Description = p.Description,
        //         SeriesCount = p.SeriesMetadataPeople.Count,
        //         IssueCount = p.ChapterPeople.Count
        //     })
        //     .OrderBy(p => p.Name);
        //
        // return await PagedList<BrowsePersonDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);

        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);

        var query = _context.Person
            .Where(p => p.SeriesMetadataPeople.Any(smp => smp.Role == PersonRole.Writer)) // Filter by role in series
            .RestrictAgainstAgeRestriction(ageRating)
            .Select(p => new BrowsePersonDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                SeriesCount = p.SeriesMetadataPeople
                    .Where(smp => smp.Role == PersonRole.Writer)
                    .Select(smp => smp.SeriesMetadata.SeriesId)
                    .Distinct()
                    .Count(),
                IssueCount = p.ChapterPeople
                    .Where(cp => cp.Role == PersonRole.Writer)
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
