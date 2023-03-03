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
            DbFactory.Genre("Action"),
            DbFactory.Genre("action"),
            DbFactory.Genre("Sci-fi"),
        };
        var genreAdded = new List<Genre>();

        GenreHelper.UpdateGenre(allGenres, new[] {"Action", "Adventure"}, genre =>
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
            DbFactory.Genre("Action"),
            DbFactory.Genre("action"),
            DbFactory.Genre("Sci-fi"),

        };
        var genreAdded = new List<Genre>();

        GenreHelper.UpdateGenre(allGenres, new[] {"Action", "Scifi"}, genre =>
        {
            genreAdded.Add(genre);
        });

        Assert.Equal(3, allGenres.Count);
        Assert.Equal(2, genreAdded.Count);
    }

    [Fact]
    public void AddGenre_ShouldAddOnlyNonExistingGenre()
    {
        var existingGenres = new List<Genre>
        {
            DbFactory.Genre("Action"),
            DbFactory.Genre("action"),
            DbFactory.Genre("Sci-fi"),
        };


        GenreHelper.AddGenreIfNotExists(existingGenres, DbFactory.Genre("Action"));
        Assert.Equal(3, existingGenres.Count);

        GenreHelper.AddGenreIfNotExists(existingGenres, DbFactory.Genre("action"));
        Assert.Equal(3, existingGenres.Count);

        GenreHelper.AddGenreIfNotExists(existingGenres, DbFactory.Genre("Shonen"));
        Assert.Equal(4, existingGenres.Count);
    }

    [Fact]
    public void KeepOnlySamePeopleBetweenLists()
    {
        var existingGenres = new List<Genre>
        {
            DbFactory.Genre("Action"),
            DbFactory.Genre("Sci-fi"),
        };

        var peopleFromChapters = new List<Genre>
        {
            DbFactory.Genre("Action"),
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
            DbFactory.Genre("Action"),
            DbFactory.Genre("Sci-fi"),
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
