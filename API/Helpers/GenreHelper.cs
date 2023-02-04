using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Entities;

namespace API.Helpers;

public static class GenreHelper
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="allGenres"></param>
    /// <param name="names"></param>
    /// <param name="action"></param>
    public static void UpdateGenre(ICollection<Genre> allGenres, IEnumerable<string> names, Action<Genre> action)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name.Trim())) continue;

            var normalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(name);
            var genre = allGenres.FirstOrDefault(p => p.NormalizedTitle.Equals(normalizedName));
            if (genre == null)
            {
                genre = DbFactory.Genre(name);
                allGenres.Add(genre);
            }

            action(genre);
        }
    }


    public static void KeepOnlySameGenreBetweenLists(ICollection<Genre> existingGenres, ICollection<Genre> removeAllExcept, Action<Genre> action = null)
    {
        var existing = existingGenres.ToList();
        foreach (var genre in existing)
        {
            var existingPerson = removeAllExcept.FirstOrDefault(g => genre.NormalizedTitle.Equals(g.NormalizedTitle));
            if (existingPerson != null) continue;
            existingGenres.Remove(genre);
            action?.Invoke(genre);
        }

    }

    /// <summary>
    /// Adds the genre to the list if it's not already in there. This will ignore the ExternalTag.
    /// </summary>
    /// <param name="metadataGenres"></param>
    /// <param name="genre"></param>
    public static void AddGenreIfNotExists(ICollection<Genre> metadataGenres, Genre genre)
    {
        var existingGenre = metadataGenres.FirstOrDefault(p =>
            p.NormalizedTitle == Services.Tasks.Scanner.Parser.Parser.Normalize(genre.Title));
        if (existingGenre == null)
        {
            metadataGenres.Add(genre);
        }
    }
}
