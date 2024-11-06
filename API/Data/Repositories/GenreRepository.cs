using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IGenreRepository
{
    void Attach(Genre genre);
    void Remove(Genre genre);
    Task<Genre?> FindByNameAsync(string genreName);
    Task<IList<Genre>> GetAllGenresAsync();
    Task<IList<Genre>> GetAllGenresByNamesAsync(IEnumerable<string> normalizedNames);
    Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false);
    Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null, QueryContext context = QueryContext.None);
    Task<int> GetCountAsync();
    Task<GenreTagDto> GetRandomGenre();
    Task<GenreTagDto> GetGenreById(int id);
    Task<List<string>> GetAllGenresNotInListAsync(ICollection<string> genreNames);
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

    public async Task<Genre?> FindByNameAsync(string genreName)
    {
        var normalizedName = genreName.ToNormalized();
        return await _context.Genre
            .FirstOrDefaultAsync(g => g.NormalizedTitle != null && g.NormalizedTitle.Equals(normalizedName));
    }

    public async Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false)
    {
        var genresWithNoConnections = await _context.Genre
            .Include(p => p.SeriesMetadatas)
            .Include(p => p.Chapters)
            .Where(p => p.SeriesMetadatas.Count == 0 && p.Chapters.Count == 0)
            .AsSplitQuery()
            .ToListAsync();

        _context.Genre.RemoveRange(genresWithNoConnections);

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.Genre.CountAsync();
    }

    public async Task<GenreTagDto> GetRandomGenre()
    {
        var genreCount = await GetCountAsync();
        if (genreCount == 0) return null;

        var randomIndex = new Random().Next(0, genreCount);
        return await _context.Genre
            .Skip(randomIndex)
            .Take(1)
            .ProjectTo<GenreTagDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<GenreTagDto> GetGenreById(int id)
    {
        return await _context.Genre
            .Where(g => g.Id == id)
            .ProjectTo<GenreTagDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<Genre>> GetAllGenresAsync()
    {
        return await _context.Genre.ToListAsync();
    }

    public async Task<IList<Genre>> GetAllGenresByNamesAsync(IEnumerable<string> normalizedNames)
    {
        return await _context.Genre
            .Where(g => normalizedNames.Contains(g.NormalizedTitle))
            .ToListAsync();
    }

    /// <summary>
    /// Returns a set of Genre tags for a set of library Ids.
    /// UserId will restrict returned Genres based on user's age restriction and library access.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryIds"></param>
    /// <returns></returns>
    public async Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null, QueryContext context = QueryContext.None)
    {
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = await _context.Library.GetUserLibraries(userId, context).ToListAsync();

        if (libraryIds is {Count: > 0})
        {
            userLibs = userLibs.Where(libraryIds.Contains).ToList();
        }

        return await _context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .SelectMany(s => s.Metadata.Genres)
            .AsSplitQuery()
            .Distinct()
            .OrderBy(p => p.NormalizedTitle)
            .ProjectTo<GenreTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all genres that are not already present in the system.
    /// Normalizes genres for lookup, but returns non-normalized names for creation.
    /// </summary>
    /// <param name="genreNames">The list of genre names (non-normalized).</param>
    /// <returns>A list of genre names that do not exist in the system.</returns>
    public async Task<List<string>> GetAllGenresNotInListAsync(ICollection<string> genreNames)
    {
        // Group the genres by their normalized names, keeping track of the original names
        var normalizedToOriginalMap = genreNames
            .Distinct()
            .GroupBy(Parser.Normalize)
            .ToDictionary(group => group.Key, group => group.First()); // Take the first original name for each normalized name

        var normalizedGenreNames = normalizedToOriginalMap.Keys.ToList();

        // Query the database for existing genres using the normalized names
        var existingGenres = await _context.Genre
            .Where(g => normalizedGenreNames.Contains(g.NormalizedTitle)) // Assuming you have a normalized field
            .Select(g => g.NormalizedTitle)
            .ToListAsync();

        // Find the normalized genres that do not exist in the database
        var missingGenres = normalizedGenreNames.Except(existingGenres).ToList();

        // Return the original non-normalized genres for the missing ones
        return missingGenres.Select(normalizedName => normalizedToOriginalMap[normalizedName]).ToList();
    }
}
