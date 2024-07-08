﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using API.Extensions.QueryExtensions;
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
    Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null);
    Task<int> GetCountAsync();
    Task<GenreTagDto> GetRandomGenre();
    Task<GenreTagDto> GetGenreById(int id);
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
    public async Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null)
    {
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = await _context.Library.GetUserLibraries(userId).ToListAsync();

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
}
