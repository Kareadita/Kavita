using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Metadata.Browse;
using Kavita.Models.Entities;
using Kavita.Models.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class GenreRepository(DataContext context, IMapper mapper) : IGenreRepository
{
    public void Attach(Genre genre)
    {
        context.Genre.Attach(genre);
    }

    public void Remove(Genre genre)
    {
        context.Genre.Remove(genre);
    }

    public async Task<Genre?> FindByNameAsync(string genreName, CancellationToken ct = default)
    {
        var normalizedName = genreName.ToNormalized();
        return await context.Genre
            .FirstOrDefaultAsync(g => g.NormalizedTitle != null && g.NormalizedTitle.Equals(normalizedName), cancellationToken: ct);
    }

    public async Task RemoveAllGenreNoLongerAssociated(bool removeExternal = false, CancellationToken ct = default)
    {
        var genresWithNoConnections = await context.Genre
            .Include(p => p.SeriesMetadatas)
            .Include(p => p.Chapters)
            .Where(p => p.SeriesMetadatas.Count == 0 && p.Chapters.Count == 0)
            .AsSplitQuery()
            .ToListAsync(cancellationToken: ct);

        context.Genre.RemoveRange(genresWithNoConnections);

        await context.SaveChangesAsync(ct);
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        return await context.Genre.CountAsync(cancellationToken: ct);
    }

    public async Task<GenreTagDto?> GetRandomGenre(CancellationToken ct = default)
    {
        var genreCount = await GetCountAsync(ct);
        if (genreCount == 0) return null;

        var randomIndex = new Random().Next(0, genreCount);
        return await context.Genre
            .Skip(randomIndex)
            .Take(1)
            .ProjectTo<GenreTagDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<GenreTagDto?> GetGenreById(int id, CancellationToken ct = default)
    {
        return await context.Genre
            .Where(g => g.Id == id)
            .ProjectTo<GenreTagDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IList<Genre>> GetAllGenresAsync(CancellationToken ct = default)
    {
        return await context.Genre.ToListAsync(ct);
    }

    public async Task<IList<Genre>> GetAllGenresByNamesAsync(IEnumerable<string> normalizedNames,
        CancellationToken ct = default)
    {
        return await context.Genre
            .Where(g => normalizedNames.Contains(g.NormalizedTitle))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns a set of Genre tags for a set of library Ids.
    /// AppUserId will restrict returned Genres based on user's age restriction and library access.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryIds"></param>
    /// <param name="context1"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<GenreTagDto>> GetAllGenreDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null,
        QueryContext context1 = QueryContext.None, CancellationToken ct = default)
    {
        var userRating = await context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = await context.Library.GetUserLibraries(userId, context1).ToListAsync(ct);

        if (libraryIds is {Count: > 0})
        {
            userLibs = userLibs.Where(libraryIds.Contains).ToList();
        }

        return await context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .SelectMany(s => s.Metadata.Genres)
            .AsSplitQuery()
            .Distinct()
            .OrderBy(p => p.NormalizedTitle)
            .ProjectTo<GenreTagDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Gets all genres that are not already present in the system.
    /// Normalizes genres for lookup, but returns non-normalized names for creation.
    /// </summary>
    /// <param name="genreNames">The list of genre names (non-normalized).</param>
    /// <param name="ct"></param>
    /// <returns>A list of genre names that do not exist in the system.</returns>
    public async Task<List<string>> GetAllGenresNotInListAsync(ICollection<string> genreNames,
        CancellationToken ct = default)
    {
        // Group the genres by their normalized names, keeping track of the original names
        var normalizedToOriginalMap = genreNames
            .Distinct()
            .GroupBy(g => g.ToNormalized())
            .ToDictionary(group => group.Key, group => group.First()); // Take the first original name for each normalized name

        var normalizedGenreNames = normalizedToOriginalMap.Keys.ToList();

        // Query the database for existing genres using the normalized names
        var existingGenres = await context.Genre
            .Where(g => normalizedGenreNames.Contains(g.NormalizedTitle)) // Assuming you have a normalized field
            .Select(g => g.NormalizedTitle)
            .ToListAsync(ct);

        // Find the normalized genres that do not exist in the database
        var missingGenres = normalizedGenreNames.Except(existingGenres).ToList();

        // Return the original non-normalized genres for the missing ones
        return missingGenres.Select(normalizedName => normalizedToOriginalMap[normalizedName]).ToList();
    }

    public async Task<PagedList<BrowseGenreDto>> GetBrowseableGenre(int userId, UserParams userParams,
        CancellationToken ct = default)
    {
        var ageRating = await context.AppUser.GetUserAgeRestriction(userId);

        var allLibrariesCount = await context.Library.CountAsync(ct);
        var userLibs = await context.Library.GetUserLibraries(userId).ToListAsync(ct);

        var seriesIds = await context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .Select(s => s.Id).ToListAsync(ct);

        var query = context.Genre
            .RestrictAgainstAgeRestriction(ageRating)
            .WhereIf(allLibrariesCount != userLibs.Count,
                genre => genre.Chapters.Any(cp => seriesIds.Contains(cp.Volume.SeriesId)) ||
                         genre.SeriesMetadatas.Any(sm => seriesIds.Contains(sm.SeriesId)))
            .Select(g => new BrowseGenreDto
            {
                Id = g.Id,
                Title = g.Title,
                SeriesCount = g.SeriesMetadatas
                    .Where(sm => allLibrariesCount == userLibs.Count || seriesIds.Contains(sm.SeriesId))
                    .RestrictAgainstAgeRestriction(ageRating)
                    .Distinct()
                    .Count(),
                ChapterCount = g.Chapters
                    .Where(cp => allLibrariesCount == userLibs.Count || seriesIds.Contains(cp.Volume.SeriesId))
                    .RestrictAgainstAgeRestriction(ageRating)
                    .Distinct()
                    .Count(),
            })
            .OrderBy(g => g.Title);

        return await PagedList<BrowseGenreDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize, ct);
    }
}
