using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using API.Helpers.Builders;
using Microsoft.EntityFrameworkCore;

namespace API.Helpers;
#nullable enable


public static class GenreHelper
{

    public static async Task UpdateChapterGenres(Chapter chapter, IEnumerable<string> genreNames, IUnitOfWork unitOfWork)
    {
        // Normalize genre names once and store them in a hash set for quick lookups
        var normalizedGenresToAdd = new HashSet<string>(genreNames.Select(g => g.ToNormalized()));

        // Remove genres that are no longer in the new list
        var genresToRemove = chapter.Genres
            .Where(g => !normalizedGenresToAdd.Contains(g.NormalizedTitle))
            .ToList();

        if (genresToRemove.Count > 0)
        {
            foreach (var genreToRemove in genresToRemove)
            {
                chapter.Genres.Remove(genreToRemove);
            }
        }

        // Get all normalized titles to query the database for existing genres
        var existingGenreTitles = await unitOfWork.DataContext.Genre
            .Where(g => normalizedGenresToAdd.Contains(g.NormalizedTitle))
            .ToDictionaryAsync(g => g.NormalizedTitle);

        // Find missing genres that are not in the database
        var missingGenres = normalizedGenresToAdd
            .Where(nt => !existingGenreTitles.ContainsKey(nt))
            .Select(title => new GenreBuilder(title).Build())
            .ToList();

        // Add missing genres to the database
        if (missingGenres.Count > 0)
        {
            unitOfWork.DataContext.Genre.AddRange(missingGenres);
            await unitOfWork.CommitAsync();

            // Add newly inserted genres to existing genres dictionary for easier lookup
            foreach (var genre in missingGenres)
            {
                existingGenreTitles[genre.NormalizedTitle] = genre;
            }
        }

        // Add genres that are either existing or newly added to the chapter
        foreach (var normalizedTitle in normalizedGenresToAdd)
        {
            var genre = existingGenreTitles[normalizedTitle];

            if (!chapter.Genres.Contains(genre))
            {
                chapter.Genres.Add(genre);
            }
        }
    }


    public static void UpdateGenreList(ICollection<GenreTagDto>? existingGenres, Series series,
        IReadOnlyCollection<Genre> newGenres, Action<Genre> handleAdd, Action onModified)
    {
        UpdateGenreList(existingGenres.DefaultIfEmpty().Select(t => t.Title).ToList(), series, newGenres, handleAdd, onModified);
    }

    public static void UpdateGenreList(ICollection<string>? existingGenres, Series series,
        IReadOnlyCollection<Genre> newGenres, Action<Genre> handleAdd, Action onModified)
    {
        if (existingGenres == null) return;

        var isModified = false;

        // Convert tags and existing genres to hash sets for quick lookups by normalized title
        var tagSet = new HashSet<string>(existingGenres.Select(t => t.ToNormalized()));
        var genreSet = new HashSet<string>(series.Metadata.Genres.Select(g => g.NormalizedTitle));

        // Remove tags that are no longer present in the input tags
        var existingTags = series.Metadata.Genres.ToList();  // Copy to avoid modifying collection while iterating
        foreach (var existing in existingTags)
        {
            if (!tagSet.Contains(existing.NormalizedTitle)) // This correctly ensures removal of non-present tags
            {
                series.Metadata.Genres.Remove(existing);
                isModified = true;
            }
        }

        // Prepare a dictionary for quick lookup of genres from the `newGenres` collection by normalized title
        var allTagsDict = newGenres.ToDictionary(t => t.NormalizedTitle);

        // Add new tags from the input list
        foreach (var tagDto in existingGenres)
        {
            var normalizedTitle = tagDto.ToNormalized();

            if (genreSet.Contains(normalizedTitle)) continue; // This prevents re-adding existing genres

            if (allTagsDict.TryGetValue(normalizedTitle, out var existingTag))
            {
                handleAdd(existingTag);  // Add existing tag from allTagsDict
            }
            else
            {
                handleAdd(new GenreBuilder(tagDto).Build());  // Add new genre if not found
            }
            isModified = true;
        }

        // Call onModified if any changes were made
        if (isModified)
        {
            onModified();
        }
    }
}
