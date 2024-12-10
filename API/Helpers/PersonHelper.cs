using System;
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
        var modification = false;

        // Get all people with the specified role from chapterPeople
        var peopleToAdd = chapterPeople
            .Where(cp => cp.Role == role)
            .Select(cp => new { cp.Person.Name, cp.Person.NormalizedName }) // Store both real and normalized names
            .ToList();

        // Prepare a HashSet for quick lookup of normalized names of people to add
        var peopleToAddSet = new HashSet<string>(peopleToAdd.Select(p => p.NormalizedName));

        // Get all existing people from metadataPeople with the specified role
        var existingMetadataPeople = metadataPeople
            .Where(mp => mp.Role == role)
            .ToList();

        // Identify people to remove from metadataPeople
        var peopleToRemove = existingMetadataPeople
            .Where(person => !peopleToAddSet.Contains(person.Person.NormalizedName))
            .ToList();

        // Remove identified people from metadataPeople
        foreach (var personToRemove in peopleToRemove)
        {
            metadataPeople.Remove(personToRemove);
            modification = true;
        }

        // Bulk fetch existing people from the repository based on normalized names
        var existingPeopleInDb = await unitOfWork.PersonRepository
            .GetPeopleByNames(peopleToAdd.Select(p => p.NormalizedName).ToList());

        // Prepare a dictionary for quick lookup of existing people by normalized name
        var existingPeopleDict = new Dictionary<string, Person>();
        foreach (var person in existingPeopleInDb)
        {
            existingPeopleDict.TryAdd(person.NormalizedName, person);
        }

        // Track the people to attach (newly created people)
        var peopleToAttach = new List<Person>();

        // Identify new people (not already in metadataPeople) to add
        foreach (var personData in peopleToAdd)
        {
            var personName = personData.Name;
            var normalizedPersonName = personData.NormalizedName;

            // Check if the person already exists in metadataPeople with the specific role
            var personAlreadyInMetadata = metadataPeople
                .Any(mp => mp.Person.NormalizedName == normalizedPersonName && mp.Role == role);

            if (!personAlreadyInMetadata)
            {
                // Check if the person exists in the database
                if (!existingPeopleDict.TryGetValue(normalizedPersonName, out var dbPerson))
                {
                    // If not, create a new Person entity using the real name
                    dbPerson = new PersonBuilder(personName).Build();
                    peopleToAttach.Add(dbPerson); // Add new person to the list to be attached
                    modification = true;
                }

                // Add the person to the SeriesMetadataPeople collection
                metadataPeople.Add(new SeriesMetadataPeople
                {
                    PersonId = dbPerson.Id,  // EF Core will automatically update this after attach
                    Person = dbPerson,
                    SeriesMetadataId = metadata.Id,
                    SeriesMetadata = metadata,
                    Role = role
                });
                modification = true;
            }
        }

        // Attach all new people in one go (EF Core will assign IDs after commit)
        if (peopleToAttach.Count != 0)
        {
            await unitOfWork.DataContext.Person.AddRangeAsync(peopleToAttach);
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
        var normalizedPeople = people.Select(p => p.ToNormalized()).Distinct().ToList(); // Ensure distinct people

        // Get all existing ChapterPeople for the role
        var existingChapterPeople = chapter.People
            .Where(cp => cp.Role == role)
            .ToList();

        // Prepare a hash set for quick lookup of existing people by normalized name
        var existingPeopleNames = new HashSet<string>(existingChapterPeople.Select(cp => cp.Person.NormalizedName));

        // Bulk select all people from the repository whose normalized names are in the provided list
        var existingPeople = await unitOfWork.PersonRepository.GetPeopleByNames(normalizedPeople);

        // Prepare a dictionary for quick lookup by normalized name
        var existingPeopleDict = new Dictionary<string, Person>();
        foreach (var person in existingPeople)
        {
            existingPeopleDict.TryAdd(person.NormalizedName, person);
        }

        // Identify people to remove (those present in ChapterPeople but not in the new list)
        foreach (var existingChapterPerson in existingChapterPeople
                     .Where(existingChapterPerson => !normalizedPeople.Contains(existingChapterPerson.Person.NormalizedName)))
        {
            chapter.People.Remove(existingChapterPerson);
            unitOfWork.PersonRepository.Remove(existingChapterPerson);
            modification = true;
        }

        // Identify new people to add
        var newPeopleNames = normalizedPeople
            .Where(p => !existingPeopleNames.Contains(p))
            .ToList();

        if (newPeopleNames.Count > 0)
        {
            // Bulk insert new people (if they don't already exist in the database)
            var newPeople = newPeopleNames
                .Where(name => !existingPeopleDict.ContainsKey(name)) // Avoid adding duplicates
                .Select(name =>
                {
                    var realName = people.First(p => p.ToNormalized() == name); // Get the original name
                    return new PersonBuilder(realName).Build(); // Use the real name for the Person entity
                })
                .ToList();

            foreach (var newPerson in newPeople)
            {
                unitOfWork.DataContext.Person.Attach(newPerson);
                existingPeopleDict[newPerson.NormalizedName] = newPerson;
            }

            await unitOfWork.CommitAsync();
            modification = true;
        }

        // Add all people (both existing and newly created) to the ChapterPeople
        foreach (var personName in normalizedPeople)
        {
            var person = existingPeopleDict[personName];

            // Check if the person with the specific role is already added to the chapter's People collection
            if (chapter.People.Any(cp => cp.PersonId == person.Id && cp.Role == role)) continue;

            chapter.People.Add(new ChapterPeople
            {
                PersonId = person.Id,
                ChapterId = chapter.Id,
                Role = role
            });
            modification = true;
        }

        // Commit the changes to remove and add people
        if (modification)
        {
            await unitOfWork.CommitAsync();
        }
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
                dto.Translators.Count != 0 ||
                dto.Teams.Count != 0 ||
                dto.Locations.Count != 0;
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
               dto.Translators.Count != 0 ||
               dto.Teams.Count != 0 ||
                dto.Locations.Count != 0;
    }
}
