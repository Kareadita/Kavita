using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using API.Helpers.Builders;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.EntityFrameworkCore;

namespace API.Helpers;
#nullable enable

public static class TagHelper
{

    public static async Task UpdateChapterTags(Chapter chapter, IEnumerable<string> tagNames, IUnitOfWork unitOfWork)
    {
        // Normalize tag names once and store them in a hash set for quick lookups
        var normalizedTagsToAdd = new HashSet<string>(tagNames.Select(t => t.ToNormalized()));
        var existingTagsSet = new HashSet<string>(chapter.Tags.Select(t => t.NormalizedTitle));

        var isModified = false;

        // Remove tags that are no longer present in the new list
        var tagsToRemove = chapter.Tags
            .Where(t => !normalizedTagsToAdd.Contains(t.NormalizedTitle))
            .ToList();

        if (tagsToRemove.Any())
        {
            foreach (var tagToRemove in tagsToRemove)
            {
                chapter.Tags.Remove(tagToRemove);
            }
            isModified = true;
        }

        // Get all normalized titles for bulk lookup from the database
        var existingTagTitles = await unitOfWork.DataContext.Tag
            .Where(t => normalizedTagsToAdd.Contains(t.NormalizedTitle))
            .ToDictionaryAsync(t => t.NormalizedTitle);

        // Find missing tags that are not already in the database
        var missingTags = normalizedTagsToAdd
            .Where(nt => !existingTagTitles.ContainsKey(nt))
            .Select(title => new TagBuilder(title).Build())
            .ToList();

        // Add missing tags to the database if any
        if (missingTags.Count != 0)
        {
            unitOfWork.DataContext.Tag.AddRange(missingTags);
            await unitOfWork.CommitAsync();  // Commit once after adding missing tags to avoid multiple DB calls
            isModified = true;

            // Update the dictionary with newly inserted tags for easier lookup
            foreach (var tag in missingTags)
            {
                existingTagTitles[tag.NormalizedTitle] = tag;
            }
        }

        // Add the new or existing tags to the chapter
        foreach (var normalizedTitle in normalizedTagsToAdd)
        {
            var tag = existingTagTitles[normalizedTitle];

            if (!existingTagsSet.Contains(normalizedTitle))
            {
                chapter.Tags.Add(tag);
                isModified = true;
            }
        }

        // Commit changes if modifications were made to the chapter's tags
        if (isModified)
        {
            await unitOfWork.CommitAsync();
        }
    }

    /// <summary>
    /// Returns a list of strings separated by ',', distinct by normalized names, already trimmed and empty entries removed.
    /// </summary>
    /// <param name="comicInfoTagSeparatedByComma"></param>
    /// <returns></returns>
    public static IList<string> GetTagValues(string comicInfoTagSeparatedByComma)
    {
        // TODO: Refactor this into an Extension
        if (string.IsNullOrEmpty(comicInfoTagSeparatedByComma))
        {
            return ImmutableList<string>.Empty;
        }

        return comicInfoTagSeparatedByComma.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .DistinctBy(Parser.Normalize)
            .ToList();
    }


    public static void UpdateTagList(ICollection<TagDto>? existingDbTags, Series series, IReadOnlyCollection<Tag> newTags, Action<Tag> handleAdd, Action onModified)
    {
        UpdateTagList(existingDbTags.Select(t => t.Title).ToList(), series, newTags, handleAdd, onModified);
    }

    public static void UpdateTagList(ICollection<string>? existingDbTags, Series series, IReadOnlyCollection<Tag> newTags, Action<Tag> handleAdd, Action onModified)
    {
        if (existingDbTags == null) return;

        var isModified = false;

        // Convert tags and existing genres to hash sets for quick lookups by normalized title
        var existingTagSet = new HashSet<string>(existingDbTags.Select(t => t.ToNormalized()));
        var dbTagSet = new HashSet<string>(series.Metadata.Tags.Select(g => g.NormalizedTitle));

        // Remove tags that are no longer present in the input tags
        var existingTagsCopy = series.Metadata.Tags.ToList();  // Copy to avoid modifying collection while iterating
        foreach (var existing in existingTagsCopy)
        {
            if (!existingTagSet.Contains(existing.NormalizedTitle)) // This correctly ensures removal of non-present tags
            {
                series.Metadata.Tags.Remove(existing);
                isModified = true;
            }
        }

        // Prepare a dictionary for quick lookup of genres from the `newTags` collection by normalized title
        var allTagsDict = newTags.ToDictionary(t => t.NormalizedTitle);

        // Add new tags from the input list
        foreach (var tagDto in existingDbTags)
        {
            var normalizedTitle = tagDto.ToNormalized();

            if (dbTagSet.Contains(normalizedTitle)) continue; // This prevents re-adding existing genres

            if (allTagsDict.TryGetValue(normalizedTitle, out var existingTag))
            {
                handleAdd(existingTag);  // Add existing tag from allTagsDict
            }
            else
            {
                handleAdd(new TagBuilder(tagDto).Build());  // Add new genre if not found
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
