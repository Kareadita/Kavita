using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Interfaces.Repositories;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
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
            throw new System.NotImplementedException();
        }

        public async Task<Person> FindByNameAsync(string name)
        {
            var normalizedName = Parser.Parser.Normalize(name);
            return await _context.Person
                .Where(p => normalizedName.Equals(p.NormalizedName))
                .SingleOrDefaultAsync();
        }

        // public Task<IEnumerable<Person>> GetAllPeopleForSeriesId(int seriesId)
        // {
        //     return await _context.SeriesMetadata
        //         .Where(s => s.Id == seriesId)
        //         .Include(s => s.People)
        //         .Where(p => normalizedName.Equals(p.NormalizedName))
        //         .SingleOrDefaultAsync();
        // }
        public async Task<IList<Person>> GetAllPeople()
        {
            return await _context.Person
                //.DistinctBy(p => p.NormalizedName)
                .ToListAsync();
        }
    }
}
