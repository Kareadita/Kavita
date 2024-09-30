using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IPersonRepository
{
    void Attach(Person person);
    void Remove(Person person);
    Task<IList<Person>> GetAllPeople();
    Task<IList<PersonDto>> GetAllPersonDtosAsync(int userId);
    Task<IList<PersonDto>> GetAllPersonDtosByRoleAsync(int userId, PersonRole role);
    Task RemoveAllPeopleNoLongerAssociated();
    Task<IList<PersonDto>> GetAllPeopleDtosForLibrariesAsync(int userId, List<int>? libraryIds = null);
    Task<int> GetCountAsync();

    Task<IList<Person>> GetAllPeopleByRoleAndNames(PersonRole role, IEnumerable<string> normalizeNames);

    Task<List<(string Name, PersonRole Role)>> GetAllPeopleNotInListAsync(
        ICollection<(string Name, PersonRole Role)> people);
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

    public async Task RemoveAllPeopleNoLongerAssociated()
    {
        var peopleWithNoConnections = await _context.Person
            .Include(p => p.SeriesMetadatas)
            .Include(p => p.ChapterMetadatas)
            .Where(p => p.SeriesMetadatas.Count == 0 && p.ChapterMetadatas.Count == 0)
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
            .SelectMany(s => s.Metadata.People)
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

    public async Task<IList<Person>> GetAllPeopleByRoleAndNames(PersonRole role, IEnumerable<string> normalizeNames)
    {
        return await _context.Person
            .Where(p => p.Role == role && normalizeNames.Contains(p.NormalizedName))
            .ToListAsync();
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
        // TODO: Figure out how to fix this lack of RBS
        //var libraryIds = await _context.Library.GetUserLibraries(userId).ToListAsync();
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
            .Where(p => p.Role == role)
            .OrderBy(p => p.Name)
            .RestrictAgainstAgeRestriction(ageRating)
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<List<(string Name, PersonRole Role)>> GetAllPeopleNotInListAsync(ICollection<(string Name, PersonRole Role)> people)
    {
        // Normalize person names and create a dictionary mapping normalized names and roles to their original names and roles
        var normalizedToOriginalMap = people.Distinct()
            .GroupBy(p => (NormalizedName: Parser.Normalize(p.Name), p.Role))
            .ToDictionary(group => group.Key, group => group.First());

        var normalizedPeople = normalizedToOriginalMap.Keys.ToList();

        // Query the database for existing people using the normalized names and roles
        var existingPeople = await _context.Person
            .Where(p => normalizedPeople
                .Select(np => new { np.NormalizedName, np.Role })
                .Contains(new { p.NormalizedName, p.Role }))
            .Select(p => new { p.NormalizedName, p.Role })
            .ToListAsync();

        // Find the normalized people (names and roles) that do not exist in the database
        var missingPeople = normalizedPeople
            .Except(existingPeople.Select(ep => (ep.NormalizedName, ep.Role)))
            .ToList();

        // Return the original non-normalized names and roles for the missing people
        return missingPeople
            .Select(normalizedPerson => normalizedToOriginalMap[normalizedPerson])
            .ToList();
    }
}
