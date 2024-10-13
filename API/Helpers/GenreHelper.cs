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
        // Normalize and build genres from the list of genre names
        var genresToAdd = genreNames
            .Select(g => new GenreBuilder(g).Build())
            .ToList();

        // Remove any genres that are not part of the new list
        var genresToRemove = chapter.Genres
            .Where(g => genresToAdd.TrueForAll(ga => ga.NormalizedTitle != g.NormalizedTitle))
            .ToList();

        foreach (var genreToRemove in genresToRemove)
        {
            chapter.Genres.Remove(genreToRemove);
        }

        // Get all normalized titles for bulk lookup
        var normalizedTitles = genresToAdd.Select(g => g.NormalizedTitle).ToList();

        // Bulk lookup for existing genres in the database
        var existingGenres = await unitOfWork.DataContext.Genre
            .Where(g => normalizedTitles.Contains(g.NormalizedTitle))
            .ToListAsync();

        // Find genres that do not exist in the database
        var missingGenres = genresToAdd
            .Where(g => existingGenres.TrueForAll(eg => eg.NormalizedTitle != g.NormalizedTitle))
            .ToList();

        // Add missing genres to the database
        if (missingGenres.Count != 0)
        {
            unitOfWork.DataContext.Genre.AddRange(missingGenres);
            await unitOfWork.CommitAsync();  // Commit the changes to the database
        }

        // Add the new or existing genres to the chapter
        foreach (var genre in genresToAdd)
        {
            var existingGenre = existingGenres.FirstOrDefault(g => g.NormalizedTitle == genre.NormalizedTitle)
                                ?? missingGenres.FirstOrDefault(g => g.NormalizedTitle == genre.NormalizedTitle);

            if (existingGenre != null && !chapter.Genres.Contains(existingGenre))
            {
                chapter.Genres.Add(existingGenre);
            }
        }
    }


    public static void UpdateGenre(Dictionary<string, Genre> allGenres,
        IEnumerable<string> names, Action<Genre, bool> action)
    {
        foreach (var name in names)
        {
            var normalizedName = name.ToNormalized();
            if (string.IsNullOrEmpty(normalizedName)) continue;

            if (allGenres.TryGetValue(normalizedName, out var genre))
            {
                action(genre, false);
            }
            else
            {
                genre = new GenreBuilder(name).Build();
                allGenres.Add(normalizedName, genre);
                action(genre, true);
            }
        }
    }

    public static void KeepOnlySameGenreBetweenLists(ICollection<Genre> existingGenres, ICollection<Genre> removeAllExcept, Action<Genre>? action = null)
    {
        var existing = existingGenres.ToList();
        foreach (var genre in existing)
        {
            var existingPerson = removeAllExcept.FirstOrDefault(g => genre.NormalizedTitle != null && genre.NormalizedTitle.Equals(g.NormalizedTitle));
            if (existingPerson != null) continue;
            existingGenres.Remove(genre);
            action?.Invoke(genre);
        }

    }

    /// <summary>
    /// Adds the genre to the list if it's not already in there.
    /// </summary>
    /// <param name="metadataGenres"></param>
    /// <param name="genre"></param>
    public static void AddGenreIfNotExists(ICollection<Genre> metadataGenres, Genre genre)
    {
        var existingGenre = metadataGenres.FirstOrDefault(p =>
            p.NormalizedTitle.Equals(genre.Title?.ToNormalized()));
        if (existingGenre == null)
        {
            metadataGenres.Add(genre);
        }
    }



    public static void UpdateGenreList(ICollection<GenreTagDto>? tags, Series series,
        IReadOnlyCollection<Genre> allTags, Action<Genre> handleAdd, Action onModified)
    {
        // TODO: Write some unit tests
        if (tags == null) return;
        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.Genres.ToList();
        foreach (var existing in existingTags)
        {
            if (tags.SingleOrDefault(t => t.Title.ToNormalized().Equals(existing.NormalizedTitle)) == null)
            {
                // Remove tag
                series.Metadata.Genres.Remove(existing);
                isModified = true;
            }
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tagTitle in tags.Select(t => t.Title))
        {
            var normalizedTitle = tagTitle.ToNormalized();
            var existingTag = allTags.SingleOrDefault(t => t.NormalizedTitle.Equals(normalizedTitle));
            if (existingTag != null)
            {
                if (series.Metadata.Genres.All(t => !t.NormalizedTitle.Equals(normalizedTitle)))
                {
                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(new GenreBuilder(tagTitle).Build());
                isModified = true;
            }
        }

        if (isModified)
        {
            onModified();
        }
    }

    public static void UpdateGenreList(ICollection<GenreTagDto>? tags, Chapter chapter,
        IReadOnlyCollection<Genre> allTags, Action<Genre> handleAdd, Action onModified)
    {
        // TODO: Write some unit tests
        if (tags == null) return;
        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = chapter.Genres.ToList();
        foreach (var existing in existingTags)
        {
            if (tags.SingleOrDefault(t => t.Title.ToNormalized().Equals(existing.NormalizedTitle)) == null)
            {
                // Remove tag
                chapter.Genres.Remove(existing);
                isModified = true;
            }
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tagTitle in tags.Select(t => t.Title))
        {
            var normalizedTitle = tagTitle.ToNormalized();
            var existingTag = allTags.SingleOrDefault(t => t.NormalizedTitle.Equals(normalizedTitle));
            if (existingTag != null)
            {
                if (chapter.Genres.All(t => !t.NormalizedTitle.Equals(normalizedTitle)))
                {
                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(new GenreBuilder(tagTitle).Build());
                isModified = true;
            }
        }

        if (isModified)
        {
            onModified();
        }
    }
}
