using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Entities.Person;
using API.Extensions;
using API.Helpers.Builders;

namespace API.Helpers;
#nullable enable

// This isn't needed in the new person architecture
public static class PersonHelper
{

    public static Dictionary<string, Person> ConstructNameAndAliasDictionary(IList<Person> people)
    {
        var dict = new Dictionary<string, Person>();
        foreach (var person in people)
        {
            dict.TryAdd(person.NormalizedName, person);
            foreach (var alias in person.Aliases)
            {
                dict.TryAdd(alias.NormalizedAlias, person);
            }
        }
        return dict;
    }

    /// <summary>
    /// Update people of a specific role for this series. Removes removed people, and adds missing ones.
    /// </summary>
    /// <param name="databasePeople">All people references in the series chapters as build by <see cref="ConstructNameAndAliasDictionary"/></param>
    /// <param name="metadata">Series metadata</param>
    /// <param name="chapterPeople">All chapter people with</param>
    /// <param name="role">The role to link with</param>
    public static void UpdateSeriesMetadataPeople(
        Dictionary<string, Person> databasePeople,
        SeriesMetadata metadata,
        IEnumerable<ChapterPeople> chapterPeople,
        PersonRole role)
    {
        var normalizedPeople = chapterPeople
            .Where(cp => cp.Role == role)
            .Select(cp => cp.Person.NormalizedName)
            .Distinct()
            .ToList();

        var existingMetadataPeople = metadata.People
            .Where(mp => mp.Role == role)
            .ToList();

        var existingPeopleNames = new HashSet<string>(
            existingMetadataPeople.Select(mp => mp.Person.NormalizedName));

        var toRemove = existingMetadataPeople
            .Where(mp => !normalizedPeople.Contains(mp.Person.NormalizedName));

        foreach (var existingMetadataPerson in toRemove)
        {
            metadata.People.Remove(existingMetadataPerson);
        }

        var newPeople = normalizedPeople
            .Where(p => !existingPeopleNames.Contains(p))
            .Select(p => databasePeople[p])
            .ToList();

        foreach (var person in newPeople)
        {
            if (metadata.People.Any(mp => mp.Person.NormalizedName == person.NormalizedName && mp.Role == role)) continue;

            metadata.People.Add(new SeriesMetadataPeople
            {
                PersonId = person.Id,
                Person = person,
                SeriesMetadataId = metadata.Id,
                SeriesMetadata = metadata,
                Role = role,
            });
        }
    }

    /// <summary>
    /// Update people of a specific role for this chapter. Removes removed people, and adds missing ones.
    /// </summary>
    /// <param name="databasePeople">All people references in the series chapters as build by <see cref="ConstructNameAndAliasDictionary"/></param>
    /// <param name="chapter">The chapter</param>
    /// <param name="comicInfoPeople">The people as defined in the chapters ComicInfo</param>
    /// <param name="role">The role to link them with</param>
    public static void UpdateChapterPeople(
        Dictionary<string, Person> databasePeople,
        Chapter chapter,
        IList<string> comicInfoPeople,
        PersonRole role)
    {
        var normalizedPeople = comicInfoPeople.Select(p => p.ToNormalized()).Distinct().ToList();

        var existingChapterPeople = chapter.People
            .Where(cp => cp.Role == role)
            .ToList();

        var existingPeopleNames = new HashSet<string>(existingChapterPeople.Select(cp => cp.Person.NormalizedName));

        var toRemove = existingChapterPeople
            .Where(p => !normalizedPeople.Contains(p.Person.NormalizedName));
        foreach (var existingChapterPerson in toRemove)
        {
            chapter.People.Remove(existingChapterPerson);
        }

        var newPeople = normalizedPeople
            .Where(p => !existingPeopleNames.Contains(p))
            .Select(p => databasePeople[p])
            .ToList();

        foreach (var person in newPeople)
        {
            if (chapter.People.Any(cp => cp.Person.NormalizedName == person.NormalizedName && cp.Role == role)) continue;

            chapter.People.Add(new ChapterPeople
            {
                PersonId = person.Id,
                Person = person,
                ChapterId = chapter.Id,
                Role = role,
            });
        }
    }

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
            .Where(person =>
                !peopleToAddSet.Contains(person.Person.NormalizedName) &&
                !person.Person.Aliases.Any(pa => peopleToAddSet.Contains(pa.NormalizedAlias)))
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
        var existingPeopleDict = ConstructNameAndAliasDictionary(existingPeopleInDb);

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
        var existingPeopleDict = ConstructNameAndAliasDictionary(existingPeople);

        // Identify people to remove (those present in ChapterPeople but not in the new list)
        var toRemove = existingChapterPeople
            .Where(existingChapterPerson => !normalizedPeople.Contains(existingChapterPerson.Person.NormalizedName));
        foreach (var existingChapterPerson in toRemove)
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
