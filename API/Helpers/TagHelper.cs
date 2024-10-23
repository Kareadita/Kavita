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
        if (missingTags.Any())
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


    public static void UpdateTagList(ICollection<TagDto>? tags, Series series, IReadOnlyCollection<Tag> allTags, Action<Tag> handleAdd, Action onModified)
    {
        if (tags == null) return;

        var isModified = false;
        var existingTags = series.Metadata.Tags;

        // Create a HashSet for quick lookup of tag IDs
        var tagIds = new HashSet<int>(tags.Select(t => t.Id));

        // Remove tags that no longer exist in the provided tag list
        var tagsToRemove = existingTags.Where(existing => !tagIds.Contains(existing.Id)).ToList();
        if (tagsToRemove.Count > 0)
        {
            foreach (var tagToRemove in tagsToRemove)
            {
                existingTags.Remove(tagToRemove);
            }
            isModified = true;
        }

        // Create a HashSet of normalized titles for quick lookups
        var normalizedTitlesToAdd = new HashSet<string>(tags.Select(t => t.Title.ToNormalized()));
        var existingNormalizedTitles = new HashSet<string>(existingTags.Select(t => t.NormalizedTitle));

        // Add missing tags based on normalized title comparison
        foreach (var normalizedTitle in normalizedTitlesToAdd)
        {
            if (existingNormalizedTitles.Contains(normalizedTitle)) continue;

            var existingTag = allTags.FirstOrDefault(t => t.NormalizedTitle == normalizedTitle);
            handleAdd(existingTag ?? new TagBuilder(normalizedTitle).Build());
            isModified = true;
        }

        // Call the modification handler if any changes were made
        if (isModified)
        {
            onModified();
        }
    }

}
