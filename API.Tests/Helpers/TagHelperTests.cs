using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Helpers;

public class TagHelperTests
{
    [Fact]
    public void UpdateTag_ShouldAddNewTag()
    {
        var allTags = new Dictionary<string, Tag>
        {
            {"Action".ToNormalized(), new TagBuilder("Action").Build()},
            {"Sci-fi".ToNormalized(), new TagBuilder("Sci-fi").Build()}
        };
        var tagCalled = new List<Tag>();
        var addedCount = 0;

        TagHelper.UpdateTag(allTags, new[] {"Action", "Adventure"}, (tag, added) =>
        {
            if (added)
            {
                addedCount++;
            }
            tagCalled.Add(tag);
        });

        Assert.Equal(1, addedCount);
        Assert.Equal(2, tagCalled.Count());
        Assert.Equal(3, allTags.Count);
    }

    [Fact]
    public void UpdateTag_ShouldNotAddDuplicateTag()
    {
        var allTags = new Dictionary<string, Tag>
        {
            {"Action".ToNormalized(), new TagBuilder("Action").Build()},
            {"Sci-fi".ToNormalized(), new TagBuilder("Sci-fi").Build()}
        };
        var tagCalled = new List<Tag>();
        var addedCount = 0;

        TagHelper.UpdateTag(allTags, new[] {"Action", "Scifi"}, (tag, added) =>
        {
            if (added)
            {
                addedCount++;
            }
            tagCalled.Add(tag);
        });

        Assert.Equal(2, allTags.Count);
        Assert.Equal(0, addedCount);
    }

    [Fact]
    public void AddTag_ShouldAddOnlyNonExistingTag()
    {
        var existingTags = new List<Tag>
        {
            new TagBuilder("Action").Build(),
            new TagBuilder("action").Build(),
            new TagBuilder("Sci-fi").Build(),
        };


        TagHelper.AddTagIfNotExists(existingTags, new TagBuilder("Action").Build());
        Assert.Equal(3, existingTags.Count);

        TagHelper.AddTagIfNotExists(existingTags, new TagBuilder("action").Build());
        Assert.Equal(3, existingTags.Count);

        TagHelper.AddTagIfNotExists(existingTags, new TagBuilder("Shonen").Build());
        Assert.Equal(4, existingTags.Count);
    }

    [Fact]
    public void KeepOnlySamePeopleBetweenLists()
    {
        var existingTags = new List<Tag>
        {
            new TagBuilder("Action").Build(),
            new TagBuilder("Sci-fi").Build(),
        };

        var peopleFromChapters = new List<Tag>
        {
            new TagBuilder("Action").Build(),
        };

        var tagRemoved = new List<Tag>();
        TagHelper.KeepOnlySameTagBetweenLists(existingTags,
            peopleFromChapters, tag =>
            {
                tagRemoved.Add(tag);
            });

        Assert.Single(tagRemoved);
    }

    [Fact]
    public void RemoveEveryoneIfNothingInRemoveAllExcept()
    {
        var existingTags = new List<Tag>
        {
            new TagBuilder("Action").Build(),
            new TagBuilder("Sci-fi").Build(),
        };

        var peopleFromChapters = new List<Tag>();

        var tagRemoved = new List<Tag>();
        TagHelper.KeepOnlySameTagBetweenLists(existingTags,
            peopleFromChapters, tag =>
            {
                tagRemoved.Add(tag);
            });

        Assert.Equal(2, tagRemoved.Count);
    }
}
