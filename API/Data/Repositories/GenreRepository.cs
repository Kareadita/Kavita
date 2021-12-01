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
            .FirstOrDefaultAsync(g => g.NormalizedTitle.Equals(normalizedName));
    }

    public async Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false)
    {
        var genresWithNoConnections = await _context.Genre
            .Include(p => p.SeriesMetadatas)
            .Include(p => p.Chapters)
            .Where(p => p.SeriesMetadatas.Count == 0 && p.Chapters.Count == 0 && p.ExternalTag == removeExternal)
            .ToListAsync();

        _context.Genre.RemoveRange(genresWithNoConnections);

        await _context.SaveChangesAsync();
    }

    public async Task<IList<Genre>> GetAllGenres()
    {
        return await _context.Genre.ToListAsync();;
    }
}
