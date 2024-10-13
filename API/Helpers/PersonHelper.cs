﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers.Builders;

namespace API.Helpers;
#nullable enable

// This isn't needed in the new person architecture
public static class PersonHelper
{

    public static async Task UpdateSeriesMetadataPeopleAsync(SeriesMetadata metadata, ICollection<SeriesMetadataPeople> metadataPeople,
        IEnumerable<ChapterPeople> chapterPeople, PersonRole role, IUnitOfWork unitOfWork)
    {
        // Normalize and group by person
        var peopleToAdd = chapterPeople
            .Where(cp => cp.Role == role)
            .Select(cp => cp.Person)
            .ToList();

        var modification = false;

        // Remove any people who are not part of the new list
        var peopleToRemove = metadataPeople
            .Where(mp => mp.Role == role && peopleToAdd.TrueForAll(p => p.NormalizedName != mp.Person.NormalizedName))
            .ToList();

        foreach (var personToRemove in peopleToRemove)
        {
            metadataPeople.Remove(personToRemove);
            modification = true;
        }

        // Add new people if they do not already exist
        foreach (var person in peopleToAdd)
        {
            var existingPerson = metadataPeople
                .FirstOrDefault(mp => mp.Person.NormalizedName == person.NormalizedName && mp.Role == role);

            if (existingPerson == null)
            {
                // Check if the person already exists in the database
                var dbPerson = await unitOfWork.PersonRepository.GetPersonByName(person.Name);

                if (dbPerson == null)
                {
                    // Create a new Person entity if not found in the database
                    dbPerson = new PersonBuilder(person.Name).Build();

                    // Attach and save the new Person entity
                    unitOfWork.DataContext.Person.Attach(dbPerson);
                    await unitOfWork.CommitAsync();
                }

                // Add the person to the SeriesMetadataPeople collection
                metadataPeople.Add(new SeriesMetadataPeople
                {
                    PersonId = dbPerson.Id,
                    Person = dbPerson,
                    SeriesMetadataId = metadata.Id,
                    SeriesMetadata = metadata,
                    Role = role
                });
                modification = true;
            }
        }

        // Commit the changes if any modifications were made
        if (modification)
        {
            await unitOfWork.CommitAsync();
        }
    }


    public static async Task UpdateSeriesMetadataPeopleAsync(SeriesMetadata metadata, ICollection<SeriesMetadataPeople> metadataPeople,
        IEnumerable<SeriesMetadataPeople> seriesPeople, PersonRole role, IUnitOfWork unitOfWork)
    {
        // Normalize and group by person
        var peopleToAdd = seriesPeople
            .Where(cp => cp.Role == role)
            .Select(cp => cp.Person)
            .ToList();

        var modification = false;

        // Remove any people who are not part of the new list
        var peopleToRemove = metadataPeople
            .Where(mp => mp.Role == role && peopleToAdd.TrueForAll(p => p.NormalizedName != mp.Person.NormalizedName))
            .ToList();

        foreach (var personToRemove in peopleToRemove)
        {
            metadataPeople.Remove(personToRemove);
            modification = true;
        }

        // Add new people if they do not already exist
        foreach (var person in peopleToAdd)
        {
            var existingPerson = metadataPeople
                .FirstOrDefault(mp => mp.Person.NormalizedName == person.NormalizedName && mp.Role == role);

            if (existingPerson == null)
            {
                // Check if the person already exists in the database
                var dbPerson = await unitOfWork.PersonRepository.GetPersonByName(person.Name);

                if (dbPerson == null)
                {
                    // Create a new Person entity if not found in the database
                    dbPerson = new PersonBuilder(person.Name).Build();

                    // Attach and save the new Person entity
                    unitOfWork.DataContext.Person.Attach(dbPerson);
                    await unitOfWork.CommitAsync();
                }

                // Add the person to the SeriesMetadataPeople collection
                metadataPeople.Add(new SeriesMetadataPeople
                {
                    PersonId = dbPerson.Id,
                    Person = dbPerson,
                    SeriesMetadataId = metadata.Id,
                    SeriesMetadata = metadata,
                    Role = role
                });
                modification = true;
            }
        }

        // Commit the changes if any modifications were made
        if (modification)
        {
            await unitOfWork.CommitAsync();
        }
    }



    public static async Task UpdateChapterPeopleAsync(Chapter chapter, IList<string> people, PersonRole role, IUnitOfWork unitOfWork)
    {
        var modification = false;
        // Normalize the input names for comparison
        var normalizedPeople = people.Select(p => p.ToNormalized()).ToList();

        // Get all existing ChapterPeople for the role
        var existingChapterPeople = chapter.People.Where(cp => cp.Role == role).ToList();

        // Remove people not in the new list
        foreach (var existingChapterPerson in existingChapterPeople)
        {
            if (!normalizedPeople.Contains(existingChapterPerson.Person.NormalizedName))
            {
                chapter.People.Remove(existingChapterPerson);
                unitOfWork.PersonRepository.Remove(existingChapterPerson);
                modification = true;
            }
        }

        // Add new people or existing ones if not already in the Chapter
        foreach (var personName in people)
        {
            var person = await unitOfWork.PersonRepository.GetPersonByName(personName);

            // If the person doesn't exist, create a new Person entity
            if (person == null)
            {
                person = new PersonBuilder(personName).Build();

                modification = true;
                unitOfWork.DataContext.Person.Attach(person);
                await unitOfWork.CommitAsync();
            }

            // Check if the person with the specific role is already added to the chapter's People collection
            var existingChapterPerson = chapter.People
                .FirstOrDefault(cp => cp.PersonId == person.Id && cp.Role == role);

            // Check if this person with the specific role already exists for the chapter
            if (existingChapterPerson == null)
            {
                var chapterPerson = new ChapterPeople
                {
                    PersonId = person.Id,
                    ChapterId = chapter.Id,
                    Role = role
                };

                chapter.People.Add(chapterPerson);
                modification = true;
            }
        }

        // Commit the changes to remove and add people
        if (modification)
        {
            await unitOfWork.CommitAsync();
        }
    }

    /// <summary>
    /// Given a list of all existing people, this will check the new names and roles and if it doesn't exist in allPeople, will create and
    /// add an entry. For each person in name, the callback will be executed.
    /// </summary>
    /// <remarks>This does not remove people if an empty list is passed into names</remarks>
    /// <remarks>This is used to add new people to a list without worrying about duplicating rows in the DB</remarks>
    /// <param name="allPeople"></param>
    /// <param name="names"></param>
    /// <param name="role"></param>
    /// <param name="action"></param>
    public static void UpdatePeople(ICollection<Person> allPeople, IEnumerable<string> names, PersonRole role, Action<Person> action)
    {
        // TODO: Implement People Support
        // var allPeopleTypeRole = allPeople.Where(p => p.Role == role).ToList();
        //
        // foreach (var name in names)
        // {
        //     var normalizedName = name.ToNormalized();
        //     // BUG: Doesn't this create a duplicate entry because allPeopleTypeRoles is a different instance?
        //     var person = allPeopleTypeRole.Find(p =>
        //         p.NormalizedName != null && p.NormalizedName.Equals(normalizedName));
        //     if (person == null)
        //     {
        //         person = new PersonBuilder(name, role).Build();
        //         allPeople.Add(person);
        //     }
        //
        //     action(person);
        // }
    }

    /// <summary>
    /// Remove people on a list for a given role
    /// </summary>
    /// <remarks>Used to remove before we update/add new people</remarks>
    /// <param name="existingPeople">Existing people on Entity</param>
    /// <param name="people">People from metadata</param>
    /// <param name="role">Role to filter on</param>
    /// <param name="action">Callback which will be executed for each person removed</param>
    public static void RemovePeople(ICollection<Person> existingPeople, IEnumerable<string> people, PersonRole role, Action<Person>? action = null)
    {
        // TODO: Implement People support
        // var normalizedPeople = people.Select(Services.Tasks.Scanner.Parser.Parser.Normalize).ToList();
        // if (normalizedPeople.Count == 0)
        // {
        //     var peopleToRemove = existingPeople.Where(p => p.Role == role).ToList();
        //     foreach (var existingRoleToRemove in peopleToRemove)
        //     {
        //         existingPeople.Remove(existingRoleToRemove);
        //         action?.Invoke(existingRoleToRemove);
        //     }
        //     return;
        // }
        //
        // foreach (var person in normalizedPeople)
        // {
        //     var existingPerson = existingPeople.FirstOrDefault(p => p.Role == role && person.Equals(p.NormalizedName));
        //     if (existingPerson == null) continue;
        //
        //     existingPeople.Remove(existingPerson);
        //     action?.Invoke(existingPerson);
        // }

    }


    /// <summary>
    /// Removes all people that are not present in the removeAllExcept list.
    /// </summary>
    /// <param name="existingPeople"></param>
    /// <param name="removeAllExcept"></param>
    /// <param name="action">Callback for all entities that should be removed</param>
    public static void KeepOnlySamePeopleBetweenLists(IEnumerable<Person> existingPeople, ICollection<Person> removeAllExcept, Action<Person>? action = null)
    {
        // TODO: Implement People support
        // foreach (var person in existingPeople)
        // {
        //     var existingPerson = removeAllExcept
        //         .FirstOrDefault(p => p.Role == person.Role && person.NormalizedName.Equals(p.NormalizedName));
        //     if (existingPerson == null)
        //     {
        //         action?.Invoke(person);
        //     }
        // }
    }

    /// <summary>
    /// Adds the person to the list if it's not already in there
    /// </summary>
    /// <param name="metadataPeople"></param>
    /// <param name="person"></param>
    // public static void AddPersonIfNotExists(ICollection<Person> metadataPeople, Person person)
    // {
    //
    //     if (string.IsNullOrEmpty(person.Name)) return;
    //     var existingPerson = metadataPeople.FirstOrDefault(p =>
    //         p.NormalizedName == person.Name.ToNormalized() && p.Role == person.Role);
    //
    //     if (existingPerson == null)
    //     {
    //         metadataPeople.Add(person);
    //     }
    // }


    public static void AddPersonIfNotExists(ICollection<SeriesMetadataPeople> metadataPeople, Person person, PersonRole role)
    {
        if (string.IsNullOrEmpty(person.Name)) return;

        // Check if the person with the specific role already exists in the collection
        var existingPerson = metadataPeople.FirstOrDefault(p =>
            p.Person.NormalizedName == person.Name.ToNormalized() && p.Role == role);

        // Add the person with the role if not already present
        if (existingPerson == null)
        {
            metadataPeople.Add(new SeriesMetadataPeople
            {
                Person = person,
                Role = role
            });
        }
    }




    /// <summary>
    /// For a given role and people dtos, update a series
    /// </summary>
    /// <param name="role"></param>
    /// <param name="people"></param>
    /// <param name="series"></param>
    /// <param name="allPeople"></param>
    /// <param name="handleAdd">This will call with an existing or new tag, but the method does not update the series Metadata</param>
    /// <param name="onModified"></param>
    public static void UpdatePeopleList(PersonRole role, ICollection<PersonDto>? people, Series series, IReadOnlyCollection<Person> allPeople,
        Action<Person> handleAdd, Action onModified)
    {
        // TODO: Implement People support
        // if (people == null) return;
        // var isModified = false;
        // // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        // var existingTags = series.Metadata.People.Where(p => p.Role == role).ToList();
        // foreach (var existing in existingTags)
        // {
        //     if (people.SingleOrDefault(t => t.Id == existing.Id) == null) // This needs to check against role
        //     {
        //         // Remove tag
        //         series.Metadata.People.Remove(existing);
        //         isModified = true;
        //     }
        // }
        //
        // // At this point, all tags that aren't in dto have been removed.
        // foreach (var tag in people)
        // {
        //     var existingTag = allPeople.FirstOrDefault(t => t.Name == tag.Name && t.Role == tag.Role);
        //     if (existingTag != null)
        //     {
        //         if (series.Metadata.People.Where(t => t.Role == tag.Role).All(t => t.Name != null && !t.Name.Equals(tag.Name)))
        //         {
        //             handleAdd(existingTag);
        //             isModified = true;
        //         }
        //     }
        //     else
        //     {
        //         // Add new tag
        //         handleAdd(new PersonBuilder(tag.Name, role).Build());
        //         isModified = true;
        //     }
        // }
        //
        // if (isModified)
        // {
        //     onModified();
        // }
    }

    public static void UpdatePeopleList(PersonRole role, ICollection<PersonDto>? people, Chapter chapter, IReadOnlyCollection<Person> allPeople,
        Action<Person> handleAdd, Action onModified)
    {
        // TODO: Implement People support
        // if (people == null) return;
        // var isModified = false;
        // // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        // var existingTags = chapter.People.Where(p => p.Role == role).ToList();
        // foreach (var existing in existingTags)
        // {
        //     if (people.SingleOrDefault(t => t.Id == existing.Id) == null) // This needs to check against role
        //     {
        //         // Remove tag
        //         chapter.People.Remove(existing);
        //         isModified = true;
        //     }
        // }
        //
        // // At this point, all tags that aren't in dto have been removed.
        // foreach (var tag in people)
        // {
        //     var existingTag = allPeople.FirstOrDefault(t => t.Name == tag.Name && t.Role == tag.Role);
        //     if (existingTag != null)
        //     {
        //         if (chapter.People.Where(t => t.Role == tag.Role).All(t => t.Name != null && !t.Name.Equals(tag.Name)))
        //         {
        //             handleAdd(existingTag);
        //             isModified = true;
        //         }
        //     }
        //     else
        //     {
        //         // Add new tag
        //         handleAdd(new PersonBuilder(tag.Name, role).Build());
        //         isModified = true;
        //     }
        // }
        //
        // if (isModified)
        // {
        //     onModified();
        // }
    }

    public static bool HasAnyPeople(SeriesMetadataDto? dto)
    {
        if (dto == null) return false;
        return dto.Writers.Count != 0 ||
                dto.CoverArtists.Count != 0 ||
                dto.Publishers.Count != 0 ||
                dto.Characters.Count != 0 ||
                dto.Pencillers.Count != 0 ||
                dto.Inkers.Count != 0 ||
                dto.Colorists.Count != 0 ||
                dto.Letterers.Count != 0 ||
                dto.Editors.Count != 0 ||
                dto.Translators.Count != 0;
    }

    public static bool HasAnyPeople(UpdateChapterDto? dto)
    {
        if (dto == null) return false;
        return dto.Writers.Count != 0 ||
               dto.CoverArtists.Count != 0 ||
               dto.Publishers.Count != 0 ||
               dto.Characters.Count != 0 ||
               dto.Pencillers.Count != 0 ||
               dto.Inkers.Count != 0 ||
               dto.Colorists.Count != 0 ||
               dto.Letterers.Count != 0 ||
               dto.Editors.Count != 0 ||
               dto.Translators.Count != 0;
    }
}
