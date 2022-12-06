using System.Collections.Generic;
using API.Data;
using API.Entities;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class GenreHelperTests
{
    [Fact]
    public void UpdateGenre_ShouldAddNewGenre()
    {
        var allGenres = new List<Genre>
        {
            DbFactory.Genre("Action", false),
            DbFactory.Genre("action", false),
            DbFactory.Genre("Sci-fi", false),
        };
        var genreAdded = new List<Genre>();

        GenreHelper.UpdateGenre(allGenres, new[] {"Action", "Adventure"}, false, genre =>
        {
            genreAdded.Add(genre);
        });

        Assert.Equal(2, genreAdded.Count);
        Assert.Equal(4, allGenres.Count);
    }

    [Fact]
    public void UpdateGenre_ShouldNotAddDuplicateGenre()
    {
        var allGenres = new List<Genre>
        {
            DbFactory.Genre("Action", false),
            DbFactory.Genre("action", false),
            DbFactory.Genre("Sci-fi", false),

        };
        var genreAdded = new List<Genre>();
        GenreHelper.UpdateGenre(allGenres, new[] {"Action", "Scifi"}, false, genre =>
        {
            genreAdded.Add(genre);
        });

        Assert.Equal(3, allGenres.Count);
    }

    [Fact]
    public void AddGenre_ShouldAddOnlyNonExistingGenre()
    {
        var existingGenres = new List<Genre>
        {
            DbFactory.Genre("Action", false),
            DbFactory.Genre("action", false),
            DbFactory.Genre("Sci-fi", false),
        };


        GenreHelper.AddGenreIfNotExists(existingGenres, DbFactory.Genre("Action", false));
        Assert.Equal(3, existingGenres.Count);

        GenreHelper.AddGenreIfNotExists(existingGenres, DbFactory.Genre("action", false));
        Assert.Equal(3, existingGenres.Count);

        GenreHelper.AddGenreIfNotExists(existingGenres, DbFactory.Genre("Shonen", false));
        Assert.Equal(4, existingGenres.Count);
    }

    [Fact]
    public void AddGenre_ShouldNotAddSameNameAndExternal()
    {
        var existingGenres = new List<Genre>
        {
            DbFactory.Genre("Action", false),
            DbFactory.Genre("action", false),
            DbFactory.Genre("Sci-fi", false),
        };


        GenreHelper.AddGenreIfNotExists(existingGenres, DbFactory.Genre("Action", true));
        Assert.Equal(3, existingGenres.Count);
    }

    [Fact]
    public void KeepOnlySamePeopleBetweenLists()
    {
        var existingGenres = new List<Genre>
        {
            DbFactory.Genre("Action", false),
            DbFactory.Genre("Sci-fi", false),
        };

        var peopleFromChapters = new List<Genre>
        {
            DbFactory.Genre("Action", false),
        };

        var genreRemoved = new List<Genre>();
        GenreHelper.KeepOnlySameGenreBetweenLists(existingGenres,
            peopleFromChapters, genre =>
            {
                genreRemoved.Add(genre);
            });

        Assert.Equal(1, genreRemoved.Count);
    }

    [Fact]
    public void RemoveEveryoneIfNothingInRemoveAllExcept()
    {
        var existingGenres = new List<Genre>
        {
            DbFactory.Genre("Action", false),
            DbFactory.Genre("Sci-fi", false),
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
