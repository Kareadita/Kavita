using System;
using System.Collections.Generic;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class PersonHelperTests
{
    [Fact]
    public void UpdatePeople_ShouldAddNewPeople()
    {
        var allPeople = new List<Person>
        {
            DbFactory.Person("Joe Shmo", PersonRole.CoverArtist),
            DbFactory.Person("Joe Shmo", PersonRole.Writer)
        };
        var peopleAdded = new List<Person>();

        PersonHelper.UpdatePeople(allPeople, new[] {"Joseph Shmo", "Sally Ann"}, PersonRole.Writer, person =>
        {
            peopleAdded.Add(person);
        });

        Assert.Equal(2, peopleAdded.Count);
        Assert.Equal(4, allPeople.Count);
    }

    [Fact]
    public void UpdatePeople_ShouldNotAddDuplicatePeople()
    {
        var allPeople = new List<Person>
        {
            DbFactory.Person("Joe Shmo", PersonRole.CoverArtist),
            DbFactory.Person("Joe Shmo", PersonRole.Writer),
            DbFactory.Person("Sally Ann", PersonRole.CoverArtist),

        };
        var peopleAdded = new List<Person>();

        PersonHelper.UpdatePeople(allPeople, new[] {"Joe Shmo", "Sally Ann"}, PersonRole.CoverArtist, person =>
        {
            peopleAdded.Add(person);
        });

        Assert.Equal(3, allPeople.Count);
    }

    [Fact]
    public void RemovePeople_ShouldRemovePeopleOfSameRole()
    {
        var existingPeople = new List<Person>
        {
            DbFactory.Person("Joe Shmo", PersonRole.CoverArtist),
            DbFactory.Person("Joe Shmo", PersonRole.Writer)
        };
        var peopleRemoved = new List<Person>();
        PersonHelper.RemovePeople(existingPeople, new[] {"Joe Shmo", "Sally Ann"}, PersonRole.Writer, person =>
        {
            peopleRemoved.Add(person);
        });

        Assert.NotEqual(existingPeople, peopleRemoved);
        Assert.Equal(1, peopleRemoved.Count);
    }

    [Fact]
    public void RemovePeople_ShouldRemovePeopleFromBothRoles()
    {
        var existingPeople = new List<Person>
        {
            DbFactory.Person("Joe Shmo", PersonRole.CoverArtist),
            DbFactory.Person("Joe Shmo", PersonRole.Writer)
        };
        var peopleRemoved = new List<Person>();
        PersonHelper.RemovePeople(existingPeople, new[] {"Joe Shmo", "Sally Ann"}, PersonRole.Writer, person =>
        {
            peopleRemoved.Add(person);
        });

        Assert.NotEqual(existingPeople, peopleRemoved);
        Assert.Equal(1, peopleRemoved.Count);

        PersonHelper.RemovePeople(existingPeople, new[] {"Joe Shmo"}, PersonRole.CoverArtist, person =>
        {
            peopleRemoved.Add(person);
        });

        Assert.Equal(0, existingPeople.Count);
        Assert.Equal(2, peopleRemoved.Count);
    }

    [Fact]
    public void RemovePeople_ShouldRemovePeopleOfSameRole_WhenNothingPassed()
    {
        var existingPeople = new List<Person>
        {
            DbFactory.Person("Joe Shmo", PersonRole.Writer),
            DbFactory.Person("Joe Shmo", PersonRole.Writer),
            DbFactory.Person("Joe Shmo", PersonRole.CoverArtist)
        };
        var peopleRemoved = new List<Person>();
        PersonHelper.RemovePeople(existingPeople, new List<string>(), PersonRole.Writer, person =>
        {
            peopleRemoved.Add(person);
        });

        Assert.NotEqual(existingPeople, peopleRemoved);
        Assert.Equal(2, peopleRemoved.Count);
    }

    [Fact]
    public void KeepOnlySamePeopleBetweenLists()
    {
        var existingPeople = new List<Person>
        {
            DbFactory.Person("Joe Shmo", PersonRole.CoverArtist),
            DbFactory.Person("Joe Shmo", PersonRole.Writer),
            DbFactory.Person("Sally", PersonRole.Writer),
        };

        var peopleFromChapters = new List<Person>
        {
            DbFactory.Person("Joe Shmo", PersonRole.CoverArtist),
        };

        var peopleRemoved = new List<Person>();
        PersonHelper.KeepOnlySamePeopleBetweenLists(existingPeople,
            peopleFromChapters, person =>
            {
                peopleRemoved.Add(person);
            });

        Assert.Equal(2, peopleRemoved.Count);
    }

    [Fact]
    public void AddPeople_ShouldAddOnlyNonExistingPeople()
    {
        var existingPeople = new List<Person>
        {
            DbFactory.Person("Joe Shmo", PersonRole.CoverArtist),
            DbFactory.Person("Joe Shmo", PersonRole.Writer),
            DbFactory.Person("Sally", PersonRole.Writer),
        };


        PersonHelper.AddPersonIfNotExists(existingPeople, DbFactory.Person("Joe Shmo", PersonRole.CoverArtist));
        Assert.Equal(3, existingPeople.Count);

        PersonHelper.AddPersonIfNotExists(existingPeople, DbFactory.Person("Joe Shmo", PersonRole.Writer));
        Assert.Equal(3, existingPeople.Count);

        PersonHelper.AddPersonIfNotExists(existingPeople, DbFactory.Person("Joe Shmo Two", PersonRole.CoverArtist));
        Assert.Equal(4, existingPeople.Count);
    }

}
