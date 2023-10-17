using System.Collections.Generic;
using API.Data;
using API.Entities;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Helpers;

public class TagHelperTests
{
    [Fact]
    public void UpdateTag_ShouldAddNewTag()
    {
        var allTags = new List<Tag>
        {
            new TagBuilder("Action").Build(),
            new TagBuilder("action").Build(),
            new TagBuilder("Sci-fi").Build(),
        };
        var tagAdded = new List<Tag>();

        TagHelper.UpdateTag(allTags, new[] {"Action", "Adventure"}, (tag, added) =>
        {
            if (added)
            {
                tagAdded.Add(tag);
            }

        });

        Assert.Single(tagAdded);
        Assert.Equal(4, allTags.Count);
    }

    [Fact]
    public void UpdateTag_ShouldNotAddDuplicateTag()
    {
        var allTags = new List<Tag>
        {
            new TagBuilder("Action").Build(),
            new TagBuilder("action").Build(),
            new TagBuilder("Sci-fi").Build(),

        };
        var tagAdded = new List<Tag>();

        TagHelper.UpdateTag(allTags, new[] {"Action", "Scifi"}, (tag, added) =>
        {
            if (added)
            {
                tagAdded.Add(tag);
            }
            TagHelper.AddTagIfNotExists(allTags, tag);
        });

        Assert.Equal(3, allTags.Count);
        Assert.Empty(tagAdded);
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
