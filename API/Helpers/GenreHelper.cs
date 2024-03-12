using System;
using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using API.Helpers.Builders;

namespace API.Helpers;
#nullable enable

public static class GenreHelper
{

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
}
