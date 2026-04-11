using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Common.Extensions;
using Kavita.Models.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services.Helpers;

public static class TagHelper
{
    /// <summary>
    /// Updates a tag collection on an entity (chapter, reading list, etc.) by adding/removing tags to match the provided names.
    /// Creates new tags in the database if they don't exist yet.
    /// </summary>
    /// <param name="entityTags">The entity's tag collection (chapter.Tags, chapter.Genres, readingList.Tags)</param>
    /// <param name="tagNames">The desired tag names</param>
    /// <param name="dbSet">The DbSet for the tag type (unitOfWork.DataContext.Tag)</param>
    /// <param name="unitOfWork">Unit of work for committing changes</param>
    /// <param name="commitIfModified">When true (default), commits relationship changes at the end if anything was added/removed</param>
    public static async Task UpdateEntityTags<T>(ICollection<T> entityTags, IEnumerable<string> tagNames,
        DbSet<T> dbSet, IUnitOfWork unitOfWork, bool commitIfModified = true) where T : class, ITag, new()
    {
        // Normalize tag names once and store them in a dictionary for quick lookups
        var normalizedToOriginal = tagNames
            .Select(t => new { Original = t, Normalized = t.ToNormalized() })
            .GroupBy(x => x.Normalized)
            .ToDictionary(g => g.Key, g => g.First().Original);

        var normalizedTagsToAdd = new HashSet<string>(normalizedToOriginal.Keys);
        var existingTagsSet = new HashSet<string>(entityTags.Select(t => t.NormalizedTitle));

        var isModified = false;

        // Remove tags that are no longer present in the new list
        var tagsToRemove = entityTags
            .Where(t => !normalizedTagsToAdd.Contains(t.NormalizedTitle))
            .ToList();

        if (tagsToRemove.Count != 0)
        {
            foreach (var tagToRemove in tagsToRemove)
            {
                entityTags.Remove(tagToRemove);
            }
            isModified = true;
        }

        // Get all normalized titles for bulk lookup from the database
        var existingDbTags = await dbSet
            .Where(t => normalizedTagsToAdd.Contains(t.NormalizedTitle))
            .ToDictionaryAsync(t => t.NormalizedTitle);

        // Find missing tags that are not already in the database
        var missingTags = normalizedTagsToAdd
            .Where(nt => !existingDbTags.ContainsKey(nt))
            .Select(nt =>
            {
                var tag = new T
                {
                    Title = normalizedToOriginal[nt].Trim(),
                    NormalizedTitle = nt
                };
                return tag;
            })
            .ToList();

        // Add missing tags to the database if any
        if (missingTags.Count != 0)
        {
            dbSet.AddRange(missingTags);
            await unitOfWork.CommitAsync();
            isModified = true;

            // Update the dictionary with newly inserted tags for easier lookup
            foreach (var tag in missingTags)
            {
                existingDbTags[tag.NormalizedTitle] = tag;
            }
        }

        // Add the new or existing tags to the entity
        foreach (var normalizedTitle in normalizedTagsToAdd)
        {
            if (existingTagsSet.Contains(normalizedTitle)) continue;

            var tag = existingDbTags[normalizedTitle];
            entityTags.Add(tag);
            isModified = true;
        }

        // Commit changes if modifications were made
        if (commitIfModified && isModified)
        {
            await unitOfWork.CommitAsync();
        }
    }

    /// <summary>
    /// Updates a tag list on a series metadata entity by matching input tag names against existing DB tags.
    /// Used for series-level tag/genre updates where the caller provides all DB tags upfront.
    /// </summary>
    /// <param name="inputTags">The desired tag names (null = no-op)</param>
    /// <param name="existingEntityTags">The entity's current tag collection (e.g. series.Metadata.Tags)</param>
    /// <param name="allDbTags">All matching tags from the database for lookup</param>
    /// <param name="handleAdd">Callback to add a tag to the entity</param>
    /// <param name="onModified">Callback when any modification is made</param>
    public static void UpdateTagList<T>(
        ICollection<string>? inputTags,
        ICollection<T> existingEntityTags,
        IReadOnlyCollection<T> allDbTags,
        Action<T> handleAdd,
        Action onModified)
        where T : class, ITag, new()
    {
        if (inputTags == null) return;

        var isModified = false;

        // Convert input tags and existing entity tags to hash sets for quick lookups by normalized title
        var inputTagSet = new HashSet<string>(inputTags.Select(t => t.ToNormalized()));
        var existingTagSet = new HashSet<string>(existingEntityTags.Select(t => t.NormalizedTitle));

        // Remove tags that are no longer present in the input
        var existingCopy = existingEntityTags.ToList();
        foreach (var existing in existingCopy)
        {
            if (!inputTagSet.Contains(existing.NormalizedTitle))
            {
                existingEntityTags.Remove(existing);
                isModified = true;
            }
        }

        // Prepare a dictionary for quick lookup from the allDbTags collection
        var allTagsDict = allDbTags.ToDictionary(t => t.NormalizedTitle);

        // Add new tags from the input list
        foreach (var tagName in inputTags)
        {
            var normalizedTitle = tagName.ToNormalized();

            if (existingTagSet.Contains(normalizedTitle)) continue;

            if (allTagsDict.TryGetValue(normalizedTitle, out var existingTag))
            {
                handleAdd(existingTag);
            }
            else
            {
                var newTag = new T
                {
                    Title = tagName.Trim(),
                    NormalizedTitle = normalizedTitle
                };
                handleAdd(newTag);
            }
            isModified = true;
        }

        if (isModified)
        {
            onModified();
        }
    }
}
