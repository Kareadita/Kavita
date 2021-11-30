using System.Collections.Generic;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class GenreHelperTests
{
    [Fact]
    public void AddGenre_ShouldAddOnlyNonExistingGenre()
    {
        var existingPeople = new List<Genre>
        {
            DbFactory.Genre("Action", false),
            DbFactory.Genre("action", false),
            DbFactory.Genre("Sci-fi", false),
        };


        GenreHelper.AddGenreIfNotExists(existingPeople, DbFactory.Genre("Joe Shmo", false));
        Assert.Equal(3, existingPeople.Count);

        GenreHelper.AddGenreIfNotExists(existingPeople, DbFactory.Genre("Joe Shmo", false));
        Assert.Equal(3, existingPeople.Count);

        GenreHelper.AddGenreIfNotExists(existingPeople, DbFactory.Genre("Joe Shmo Two", false));
        Assert.Equal(4, existingPeople.Count);
    }
}
