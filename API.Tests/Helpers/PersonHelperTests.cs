using System;
using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Helpers;

public class PersonHelperTests
{
    #region UpdatePeople
    [Fact]
    public void UpdatePeople_ShouldAddNewPeople()
    {
        var allPeople = new List<Person>
        {
            new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build(),
            new PersonBuilder("Joe Shmo", PersonRole.Writer).Build(),
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
            new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build(),
            new PersonBuilder("Joe Shmo", PersonRole.Writer).Build(),
            new PersonBuilder("Sally Ann", PersonRole.CoverArtist).Build(),

        };
        var peopleAdded = new List<Person>();

        PersonHelper.UpdatePeople(allPeople, new[] {"Joe Shmo", "Sally Ann"}, PersonRole.CoverArtist, person =>
        {
            peopleAdded.Add(person);
        });

        Assert.Equal(3, allPeople.Count);
    }
    #endregion

    #region UpdatePeopleList

    [Fact]
    public void UpdatePeopleList_NullTags_NoChanges()
    {
        // Arrange
        ICollection<PersonDto> tags = null;
        var series = new SeriesBuilder("Test Series").Build();
        var allTags = new List<Person>();
        var handleAddCalled = false;
        var onModifiedCalled = false;

        // Act
        PersonHelper.UpdatePeopleList(PersonRole.Writer, tags, series, allTags, p => handleAddCalled = true, () => onModifiedCalled = true);

        // Assert
        Assert.False(handleAddCalled);
        Assert.False(onModifiedCalled);
    }

    [Fact]
    public void UpdatePeopleList_AddNewTag_TagAddedAndOnModifiedCalled()
    {
        // Arrange
        const PersonRole role = PersonRole.Writer;
        var tags = new List<PersonDto>
        {
            new PersonDto { Id = 1, Name = "John Doe", Role = role }
        };
        var series = new SeriesBuilder("Test Series").Build();
        var allTags = new List<Person>();
        var handleAddCalled = false;
        var onModifiedCalled = false;

        // Act
        PersonHelper.UpdatePeopleList(role, tags, series, allTags, p =>
        {
            handleAddCalled = true;
            series.Metadata.People.Add(p);
        }, () => onModifiedCalled = true);

        // Assert
        Assert.True(handleAddCalled);
        Assert.True(onModifiedCalled);
        Assert.Single(series.Metadata.People);
        Assert.Equal("John Doe", series.Metadata.People.First().Name);
    }

    [Fact]
    public void UpdatePeopleList_RemoveExistingTag_TagRemovedAndOnModifiedCalled()
    {
        // Arrange
        const PersonRole role = PersonRole.Writer;
        var tags = new List<PersonDto>();
        var series = new SeriesBuilder("Test Series").Build();
        var person = new PersonBuilder("John Doe", role).Build();
        person.Id = 1;
        series.Metadata.People.Add(person);
        var allTags = new List<Person>
        {
            person
        };
        var handleAddCalled = false;
        var onModifiedCalled = false;

        // Act
        PersonHelper.UpdatePeopleList(role, tags, series, allTags, p =>
        {
            handleAddCalled = true;
            series.Metadata.People.Add(p);
        }, () => onModifiedCalled = true);

        // Assert
        Assert.False(handleAddCalled);
        Assert.True(onModifiedCalled);
        Assert.Empty(series.Metadata.People);
    }

    [Fact]
    public void UpdatePeopleList_UpdateExistingTag_OnModifiedCalled()
    {
        // Arrange
        const PersonRole role = PersonRole.Writer;
        var tags = new List<PersonDto>
        {
            new PersonDto { Id = 1, Name = "John Doe", Role = role }
        };
        var series = new SeriesBuilder("Test Series").Build();
        var person = new PersonBuilder("John Doe", role).Build();
        person.Id = 1;
        series.Metadata.People.Add(person);
        var allTags = new List<Person>
        {
            person
        };
        var handleAddCalled = false;
        var onModifiedCalled = false;

        // Act
        PersonHelper.UpdatePeopleList(role, tags, series, allTags, p =>
        {
            handleAddCalled = true;
            series.Metadata.People.Add(p);
        }, () => onModifiedCalled = true);

        // Assert
        Assert.False(handleAddCalled);
        Assert.False(onModifiedCalled);
        Assert.Single(series.Metadata.People);
        Assert.Equal("John Doe", series.Metadata.People.First().Name);
    }

    [Fact]
    public void UpdatePeopleList_NoChanges_HandleAddAndOnModifiedNotCalled()
    {
        // Arrange
        const PersonRole role = PersonRole.Writer;
        var tags = new List<PersonDto>
        {
            new PersonDto { Id = 1, Name = "John Doe", Role = role }
        };
        var series = new SeriesBuilder("Test Series").Build();
        var person = new PersonBuilder("John Doe", role).Build();
        person.Id = 1;
        series.Metadata.People.Add(person);
        var allTags = new List<Person>
        {
            new PersonBuilder("John Doe", role).Build()
        };
        var handleAddCalled = false;
        var onModifiedCalled = false;

        // Act
        PersonHelper.UpdatePeopleList(role, tags, series, allTags, p =>
        {
            handleAddCalled = true;
            series.Metadata.People.Add(p);
        }, () => onModifiedCalled = true);

        // Assert
        Assert.False(handleAddCalled);
        Assert.False(onModifiedCalled);
        Assert.Single(series.Metadata.People);
        Assert.Equal("John Doe", series.Metadata.People.First().Name);
    }



    #endregion

    #region RemovePeople
    [Fact]
    public void RemovePeople_ShouldRemovePeopleOfSameRole()
    {
        var existingPeople = new List<Person>
        {
            new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build(),
            new PersonBuilder("Joe Shmo", PersonRole.Writer).Build(),
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
            new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build(),
            new PersonBuilder("Joe Shmo", PersonRole.Writer).Build(),
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
            new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build(),
            new PersonBuilder("Joe Shmo", PersonRole.Writer).Build(),
            new PersonBuilder("Joe Shmo", PersonRole.Writer).Build(),
        };
        var peopleRemoved = new List<Person>();
        PersonHelper.RemovePeople(existingPeople, new List<string>(), PersonRole.Writer, person =>
        {
            peopleRemoved.Add(person);
        });

        Assert.NotEqual(existingPeople, peopleRemoved);
        Assert.Equal(2, peopleRemoved.Count);
    }


    #endregion

    #region KeepOnlySamePeopleBetweenLists
    [Fact]
    public void KeepOnlySamePeopleBetweenLists()
    {
        var existingPeople = new List<Person>
        {
            new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build(),
            new PersonBuilder("Joe Shmo", PersonRole.Writer).Build(),
            new PersonBuilder("Sally", PersonRole.Writer).Build(),
        };

        var peopleFromChapters = new List<Person>
        {
            new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build(),
        };

        var peopleRemoved = new List<Person>();
        PersonHelper.KeepOnlySamePeopleBetweenLists(existingPeople,
            peopleFromChapters, person =>
            {
                peopleRemoved.Add(person);
            });

        Assert.Equal(2, peopleRemoved.Count);
    }
    #endregion

    #region AddPeople

    [Fact]
    public void AddPersonIfNotExists_ShouldAddPerson_WhenPersonDoesNotExist()
    {
        // Arrange
        var metadataPeople = new List<Person>();
        var person = new PersonBuilder("John Smith", PersonRole.Character).Build();

        // Act
        PersonHelper.AddPersonIfNotExists(metadataPeople, person);

        // Assert
        Assert.Single(metadataPeople);
        Assert.Contains(person, metadataPeople);
    }

    [Fact]
    public void AddPersonIfNotExists_ShouldNotAddPerson_WhenPersonAlreadyExists()
    {
        // Arrange
        var metadataPeople = new List<Person>
        {
            new PersonBuilder("John Smith", PersonRole.Character)
                .WithId(1)
                .Build()
        };
        var person = new PersonBuilder("John Smith", PersonRole.Character).Build();
        // Act
        PersonHelper.AddPersonIfNotExists(metadataPeople, person);

        // Assert
        Assert.Single(metadataPeople);
        Assert.NotNull(metadataPeople.SingleOrDefault(p =>
            p.Name.Equals(person.Name) && p.Role == person.Role && p.NormalizedName == person.NormalizedName));
        Assert.Equal(metadataPeople.First().Id, 1);
    }

    [Fact]
    public void AddPersonIfNotExists_ShouldNotAddPerson_WhenPersonNameIsNullOrEmpty()
    {
        // Arrange
        var metadataPeople = new List<Person>();
        var person2 = new PersonBuilder(string.Empty, PersonRole.Character).Build();

        // Act
        PersonHelper.AddPersonIfNotExists(metadataPeople, person2);

        // Assert
        Assert.Empty(metadataPeople);
    }

    [Fact]
    public void AddPersonIfNotExists_ShouldAddPerson_WhenPersonNameIsDifferentButRoleIsSame()
    {
        // Arrange
        var metadataPeople = new List<Person>
        {
            new PersonBuilder("John Smith", PersonRole.Character).Build()
        };
        var person = new PersonBuilder("John Doe", PersonRole.Character).Build();

        // Act
        PersonHelper.AddPersonIfNotExists(metadataPeople, person);

        // Assert
        Assert.Equal(2, metadataPeople.Count);
        Assert.Contains(person, metadataPeople);
    }

    [Fact]
    public void AddPersonIfNotExists_ShouldAddPerson_WhenPersonNameIsSameButRoleIsDifferent()
    {
        // Arrange
        var metadataPeople = new List<Person>
        {
            new PersonBuilder("John Doe", PersonRole.Writer).Build()
        };
        var person = new PersonBuilder("John Smith", PersonRole.Character).Build();

        // Act
        PersonHelper.AddPersonIfNotExists(metadataPeople, person);

        // Assert
        Assert.Equal(2, metadataPeople.Count);
        Assert.Contains(person, metadataPeople);
    }




    [Fact]
    public void AddPeople_ShouldAddOnlyNonExistingPeople()
    {
        var existingPeople = new List<Person>
        {
            new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build(),
            new PersonBuilder("Joe Shmo", PersonRole.Writer).Build(),
            new PersonBuilder("Sally", PersonRole.Writer).Build(),
        };


        PersonHelper.AddPersonIfNotExists(existingPeople, new PersonBuilder("Joe Shmo", PersonRole.CoverArtist).Build());
        Assert.Equal(3, existingPeople.Count);

        PersonHelper.AddPersonIfNotExists(existingPeople, new PersonBuilder("Joe Shmo", PersonRole.Writer).Build());
        Assert.Equal(3, existingPeople.Count);

        PersonHelper.AddPersonIfNotExists(existingPeople, new PersonBuilder("Joe Shmo Two", PersonRole.CoverArtist).Build());
        Assert.Equal(4, existingPeople.Count);
    }

    #endregion

}
