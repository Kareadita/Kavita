using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using API.Helpers.Builders;

namespace API.Helpers;
#nullable enable

public static class TagHelper
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="allTags"></param>
    /// <param name="names"></param>
    /// <param name="action">Callback for every item. Will give said item back and a bool if item was added</param>
    public static void UpdateTag(ICollection<Tag> allTags, IEnumerable<string> names, Action<Tag, bool> action)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name.Trim())) continue;

            var added = false;
            var normalizedName = name.ToNormalized();

            var genre = allTags.FirstOrDefault(p =>
                p.NormalizedTitle.Equals(normalizedName));
            if (genre == null)
            {
                added = true;
                genre = new TagBuilder(name).Build();
                allTags.Add(genre);
            }

            action(genre, added);
        }
    }

    public static void KeepOnlySameTagBetweenLists(ICollection<Tag> existingTags, ICollection<Tag> removeAllExcept, Action<Tag>? action = null)
    {
        var existing = existingTags.ToList();
        foreach (var genre in existing)
        {
            var existingPerson = removeAllExcept.FirstOrDefault(g => genre.NormalizedTitle.Equals(g.NormalizedTitle));
            if (existingPerson != null) continue;
            existingTags.Remove(genre);
            action?.Invoke(genre);
        }

    }

    /// <summary>
    /// Adds the tag to the list if it's not already in there. This will ignore the ExternalTag.
    /// </summary>
    /// <param name="metadataTags"></param>
    /// <param name="tag"></param>
    public static void AddTagIfNotExists(ICollection<Tag> metadataTags, Tag tag)
    {
        var existingGenre = metadataTags.FirstOrDefault(p =>
            p.NormalizedTitle == tag.Title.ToNormalized());
        if (existingGenre == null)
        {
            metadataTags.Add(tag);
        }
    }

    public static void AddTagIfNotExists(BlockingCollection<Tag> metadataTags, Tag tag)
    {
        var existingGenre = metadataTags.FirstOrDefault(p =>
            p.NormalizedTitle == tag.Title.ToNormalized());
        if (existingGenre == null)
        {
            metadataTags.Add(tag);
        }
    }

    /// <summary>
    /// Remove tags on a list
    /// </summary>
    /// <remarks>Used to remove before we update/add new tags</remarks>
    /// <param name="existingTags">Existing tags on Entity</param>
    /// <param name="tags">Tags from metadata</param>
    /// <param name="action">Callback which will be executed for each tag removed</param>
    public static void RemoveTags(ICollection<Tag> existingTags, IEnumerable<string> tags, Action<Tag>? action = null)
    {
        var normalizedTags = tags.Select(Services.Tasks.Scanner.Parser.Parser.Normalize).ToList();
        foreach (var person in normalizedTags)
        {
            var existingTag = existingTags.FirstOrDefault(p => person.Equals(p.NormalizedTitle));
            if (existingTag == null) continue;

            existingTags.Remove(existingTag);
            action?.Invoke(existingTag);
        }

    }

    public static void UpdateTagList(ICollection<TagDto>? tags, Series series, IReadOnlyCollection<Tag> allTags, Action<Tag> handleAdd, Action onModified)
    {
        if (tags == null) return;

        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.Tags.ToList();
        foreach (var existing in existingTags.Where(existing => tags.SingleOrDefault(t => t.Id == existing.Id) == null))
        {
            // Remove tag
            series.Metadata.Tags.Remove(existing);
            isModified = true;
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tagTitle in tags.Select(t => t.Title))
        {
            var normalizedTitle = tagTitle.ToNormalized();
            var existingTag = allTags.SingleOrDefault(t => t.NormalizedTitle.Equals(normalizedTitle));
            if (existingTag != null)
            {
                if (series.Metadata.Tags.All(t => t.NormalizedTitle != normalizedTitle))
                {

                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(new TagBuilder(tagTitle).Build());
                isModified = true;
            }
        }

        if (isModified)
        {
            onModified();
        }
    }
}

#nullable disable
