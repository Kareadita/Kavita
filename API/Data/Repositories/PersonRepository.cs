using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IPersonRepository
{
    void Attach(Person person);
    void Remove(Person person);
    Task<IList<Person>> GetAllPeople();
    Task RemoveAllPeopleNoLongerAssociated(bool removeExternal = false);
    Task<IList<PersonDto>> GetAllPeopleDtosForLibrariesAsync(List<int> libraryIds);
    Task<int> GetCountAsync();
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

    public async Task<Person> FindByNameAsync(string name)
    {
        var normalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(name);
        return await _context.Person
            .Where(p => normalizedName.Equals(p.NormalizedName))
            .SingleOrDefaultAsync();
    }

    public async Task RemoveAllPeopleNoLongerAssociated(bool removeExternal = false)
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

    public async Task<IList<PersonDto>> GetAllPeopleDtosForLibrariesAsync(List<int> libraryIds)
    {
        return await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
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


    public async Task<IList<Person>> GetAllPeople()
    {
        return await _context.Person
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}
