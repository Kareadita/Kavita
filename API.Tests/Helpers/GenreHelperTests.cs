using System.Collections.Generic;
using API.Data;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Helpers;

public class GenreHelperTests
{
    [Fact]
    public void UpdateGenre_ShouldAddNewGenre()
    {
        var allGenres = new Dictionary<string, Genre>
        {
            {"Action".ToNormalized(), new GenreBuilder("Action").Build()},
            {"Sci-fi".ToNormalized(), new GenreBuilder("Sci-fi").Build()}
        };
        var genreAdded = new List<Genre>();
        var addedCount = 0;

        GenreHelper.UpdateGenre(allGenres, new[] {"Action", "Adventure"}, (genre, isNew) =>
        {
            if (isNew)
            {
                addedCount++;
            }
            genreAdded.Add(genre);
        });

        Assert.Equal(2, genreAdded.Count);
        Assert.Equal(1, addedCount);
        Assert.Equal(3, allGenres.Count);
    }

    [Fact]
    public void UpdateGenre_ShouldNotAddDuplicateGenre()
    {
        var allGenres = new Dictionary<string, Genre>
        {
            {"Action".ToNormalized(), new GenreBuilder("Action").Build()},
            {"Sci-fi".ToNormalized(), new GenreBuilder("Sci-fi").Build()}
        };
        var genreAdded = new List<Genre>();
        var addedCount = 0;

        GenreHelper.UpdateGenre(allGenres, new[] {"Action", "Scifi"}, (genre, isNew) =>
        {
            if (isNew)
            {
                addedCount++;
            }
            genreAdded.Add(genre);
        });

        Assert.Equal(0, addedCount);
        Assert.Equal(2, genreAdded.Count);
        Assert.Equal(2, allGenres.Count);
    }

    [Fact]
    public void AddGenre_ShouldAddOnlyNonExistingGenre()
    {
        var existingGenres = new List<Genre>
        {
            new GenreBuilder("Action").Build(),
            new GenreBuilder("action").Build(),
            new GenreBuilder("Sci-fi").Build(),
        };


        GenreHelper.AddGenreIfNotExists(existingGenres, new GenreBuilder("Action").Build());
        Assert.Equal(3, existingGenres.Count);

        GenreHelper.AddGenreIfNotExists(existingGenres, new GenreBuilder("action").Build());
        Assert.Equal(3, existingGenres.Count);

        GenreHelper.AddGenreIfNotExists(existingGenres, new GenreBuilder("Shonen").Build());
        Assert.Equal(4, existingGenres.Count);
    }

    [Fact]
    public void KeepOnlySamePeopleBetweenLists()
    {
        var existingGenres = new List<Genre>
        {
            new GenreBuilder("Action").Build(),
            new GenreBuilder("Sci-fi").Build(),
        };

        var peopleFromChapters = new List<Genre>
        {
            new GenreBuilder("Action").Build(),
        };

        var genreRemoved = new List<Genre>();
        GenreHelper.KeepOnlySameGenreBetweenLists(existingGenres,
            peopleFromChapters, genre =>
            {
                genreRemoved.Add(genre);
            });

        Assert.Single(genreRemoved);
    }

    [Fact]
    public void RemoveEveryoneIfNothingInRemoveAllExcept()
    {
        var existingGenres = new List<Genre>
        {
            new GenreBuilder("Action").Build(),
            new GenreBuilder("Sci-fi").Build(),
        };

        var peopleFromChapters = new List<Genre>();

        var genreRemoved = new List<Genre>();
        GenreHelper.KeepOnlySameGenreBetweenLists(existingGenres,
            peopleFromChapters, genre =>
            {
                genreRemoved.Add(genre);
            });

        Assert.Equal(2, genreRemoved.Count);
    }
}
