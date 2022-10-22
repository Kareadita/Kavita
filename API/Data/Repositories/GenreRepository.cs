using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IGenreRepository
{
    void Attach(Genre genre);
    void Remove(Genre genre);
    Task<Genre> FindByNameAsync(string genreName);
    Task<IList<Genre>> GetAllGenresAsync();
    Task<IList<GenreTagDto>> GetAllGenreDtosAsync(int userId);
    Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false);
    Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(IList<int> libraryIds, int userId);
    Task<int> GetCountAsync();
}

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
        var normalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(genreName);
        return await _context.Genre
            .FirstOrDefaultAsync(g => g.NormalizedTitle.Equals(normalizedName));
    }

    public async Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false)
    {
        var genresWithNoConnections = await _context.Genre
            .Include(p => p.SeriesMetadatas)
            .Include(p => p.Chapters)
            .Where(p => p.SeriesMetadatas.Count == 0 && p.Chapters.Count == 0 && p.ExternalTag == removeExternal)
            .AsSplitQuery()
            .ToListAsync();

        _context.Genre.RemoveRange(genresWithNoConnections);

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Returns a set of Genre tags for a set of library Ids. UserId will restrict returned Genres based on user's age restriction.
    /// </summary>
    /// <param name="libraryIds"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(IList<int> libraryIds, int userId)
    {
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.Series
            .Where(s => libraryIds.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .SelectMany(s => s.Metadata.Genres)
            .AsSplitQuery()
            .Distinct()
            .OrderBy(p => p.Title)
            .ProjectTo<GenreTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }


    public async Task<int> GetCountAsync()
    {
        return await _context.Genre.CountAsync();
    }

    public async Task<IList<Genre>> GetAllGenresAsync()
    {
        return await _context.Genre.ToListAsync();
    }

    public async Task<IList<GenreTagDto>> GetAllGenreDtosAsync(int userId)
    {
        var ageRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.Genre
            .RestrictAgainstAgeRestriction(ageRating)
            .AsNoTracking()
            .ProjectTo<GenreTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}
