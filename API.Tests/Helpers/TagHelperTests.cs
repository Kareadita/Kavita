using System.Collections.Generic;
using API.Data;
using API.Entities;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class TagHelperTests
{
    [Fact]
    public void UpdateTag_ShouldAddNewTag()
    {
        var allTags = new List<Tag>
        {
            DbFactory.Tag("Action", false),
            DbFactory.Tag("action", false),
            DbFactory.Tag("Sci-fi", false),
        };
        var tagAdded = new List<Tag>();

        TagHelper.UpdateTag(allTags, new[] {"Action", "Adventure"}, false, (tag, added) =>
        {
            if (added)
            {
                tagAdded.Add(tag);
            }

        });

        Assert.Equal(1, tagAdded.Count);
        Assert.Equal(4, allTags.Count);
    }

    [Fact]
    public void UpdateTag_ShouldNotAddDuplicateTag()
    {
        var allTags = new List<Tag>
        {
            DbFactory.Tag("Action", false),
            DbFactory.Tag("action", false),
            DbFactory.Tag("Sci-fi", false),

        };
        var tagAdded = new List<Tag>();

        TagHelper.UpdateTag(allTags, new[] {"Action", "Scifi"}, false, (tag, added) =>
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
            DbFactory.Tag("Action", false),
            DbFactory.Tag("action", false),
            DbFactory.Tag("Sci-fi", false),
        };


        TagHelper.AddTagIfNotExists(existingTags, DbFactory.Tag("Action", false));
        Assert.Equal(3, existingTags.Count);

        TagHelper.AddTagIfNotExists(existingTags, DbFactory.Tag("action", false));
        Assert.Equal(3, existingTags.Count);

        TagHelper.AddTagIfNotExists(existingTags, DbFactory.Tag("Shonen", false));
        Assert.Equal(4, existingTags.Count);
    }

    [Fact]
    public void AddTag_ShouldNotAddSameNameAndExternal()
    {
        var existingTags = new List<Tag>
        {
            DbFactory.Tag("Action", false),
            DbFactory.Tag("action", false),
            DbFactory.Tag("Sci-fi", false),
        };


        TagHelper.AddTagIfNotExists(existingTags, DbFactory.Tag("Action", true));
        Assert.Equal(3, existingTags.Count);
    }

    [Fact]
    public void KeepOnlySamePeopleBetweenLists()
    {
        var existingTags = new List<Tag>
        {
            DbFactory.Tag("Action", false),
            DbFactory.Tag("Sci-fi", false),
        };

        var peopleFromChapters = new List<Tag>
        {
            DbFactory.Tag("Action", false),
        };

        var tagRemoved = new List<Tag>();
        TagHelper.KeepOnlySameTagBetweenLists(existingTags,
            peopleFromChapters, tag =>
            {
                tagRemoved.Add(tag);
            });

        Assert.Equal(1, tagRemoved.Count);
    }

    [Fact]
    public void RemoveEveryoneIfNothingInRemoveAllExcept()
    {
        var existingTags = new List<Tag>
        {
            DbFactory.Tag("Action", false),
            DbFactory.Tag("Sci-fi", false),
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
