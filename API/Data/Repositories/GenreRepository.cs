using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Interfaces.Repositories;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public class GenreRepository : IGenreRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public GenreRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(Genre genre)
    {
        _context.Genre.Attach(genre);
    }

    public void Remove(Genre genre)
    {
        _context.Genre.Remove(genre);
    }

    public async Task<Genre> FindByNameAsync(string genreName)
    {
        var normalizedName = Parser.Parser.Normalize(genreName);
        return await _context.Genre
            .FirstOrDefaultAsync(g => g.NormalizedName.Equals(normalizedName));
    }

    public async Task<IList<Genre>> GetAllGenres()
    {
        return await _context.Genre.ToListAsync();;
    }
}
