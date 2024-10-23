using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Tasks.Scanner.Parser;
using Xunit;

namespace API.Tests.Helpers;

public class PersonHelperTests : AbstractDbTest
{
    protected override async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());
        await _context.SaveChangesAsync();
    }
    //
    // // 1. Test adding new people and keeping existing ones
    // [Fact]
    // public async Task UpdateChapterPeopleAsync_AddNewPeople_ExistingPersonRetained()
    // {
    //     var existingPerson = new PersonBuilder("Joe Shmo").Build();
    //     var chapter = new ChapterBuilder("1").Build();
    //
    //     // Create an existing person and assign them to the series with a role
    //     var series = new SeriesBuilder("Test 1")
    //         .WithFormat(MangaFormat.Archive)
    //         .WithMetadata(new SeriesMetadataBuilder()
    //             .WithPerson(existingPerson, PersonRole.Editor)
    //             .Build())
    //         .WithVolume(new VolumeBuilder("1").WithChapter(chapter).Build())
    //         .Build();
    //
    //     _unitOfWork.SeriesRepository.Add(series);
    //     await _unitOfWork.CommitAsync();
    //
    //     // Call UpdateChapterPeopleAsync with one existing and one new person
    //     await PersonHelper.UpdateChapterPeopleAsync(chapter, new List<string> { "Joe Shmo", "New Person" }, PersonRole.Editor, _unitOfWork);
    //
    //     // Assert existing person retained and new person added
    //     var people = await _unitOfWork.PersonRepository.GetAllPeople();
    //     Assert.Contains(people, p => p.Name == "Joe Shmo");
    //     Assert.Contains(people, p => p.Name == "New Person");
    //
    //     var chapterPeople = chapter.People.Select(cp => cp.Person.Name).ToList();
    //     Assert.Contains("Joe Shmo", chapterPeople);
    //     Assert.Contains("New Person", chapterPeople);
    // }
    //
    // // 2. Test removing a person no longer in the list
    // [Fact]
    // public async Task UpdateChapterPeopleAsync_RemovePeople()
    // {
    //     var existingPerson1 = new PersonBuilder("Joe Shmo").Build();
    //     var existingPerson2 = new PersonBuilder("Jane Doe").Build();
    //     var chapter = new ChapterBuilder("1").Build();
    //
    //     var series = new SeriesBuilder("Test 1")
    //         .WithVolume(new VolumeBuilder("1")
    //             .WithChapter(new ChapterBuilder("1")
    //                 .WithPerson(existingPerson1, PersonRole.Editor)
    //                 .WithPerson(existingPerson2, PersonRole.Editor)
    //                 .Build())
    //             .Build())
    //         .Build();
    //
    //     _unitOfWork.SeriesRepository.Add(series);
    //     await _unitOfWork.CommitAsync();
    //
    //     // Call UpdateChapterPeopleAsync with only one person
    //     await PersonHelper.UpdateChapterPeopleAsync(chapter, new List<string> { "Joe Shmo" }, PersonRole.Editor, _unitOfWork);
    //
    //     var people = await _unitOfWork.PersonRepository.GetAllPeople();
    //     Assert.DoesNotContain(people, p => p.Name == "Jane Doe");
    //
    //     var chapterPeople = chapter.People.Select(cp => cp.Person.Name).ToList();
    //     Assert.Contains("Joe Shmo", chapterPeople);
    //     Assert.DoesNotContain("Jane Doe", chapterPeople);
    // }
    //
    // // 3. Test no changes when the list of people is the same
    // [Fact]
    // public async Task UpdateChapterPeopleAsync_NoChanges()
    // {
    //     var existingPerson = new PersonBuilder("Joe Shmo").Build();
    //     var chapter = new ChapterBuilder("1").Build();
    //
    //     var series = new SeriesBuilder("Test 1")
    //         .WithVolume(new VolumeBuilder("1")
    //             .WithChapter(new ChapterBuilder("1")
    //                 .WithPerson(existingPerson, PersonRole.Editor)
    //                 .Build())
    //             .Build())
    //         .Build();
    //
    //     _unitOfWork.SeriesRepository.Add(series);
    //     await _unitOfWork.CommitAsync();
    //
    //     // Call UpdateChapterPeopleAsync with the same list
    //     await PersonHelper.UpdateChapterPeopleAsync(chapter, new List<string> { "Joe Shmo" }, PersonRole.Editor, _unitOfWork);
    //
    //     var people = await _unitOfWork.PersonRepository.GetAllPeople();
    //     Assert.Contains(people, p => p.Name == "Joe Shmo");
    //
    //     var chapterPeople = chapter.People.Select(cp => cp.Person.Name).ToList();
    //     Assert.Contains("Joe Shmo", chapterPeople);
    //     Assert.Single(chapter.People); // No duplicate entries
    // }
    //
    // // 4. Test multiple roles for a person
    // [Fact]
    // public async Task UpdateChapterPeopleAsync_MultipleRoles()
    // {
    //     var person = new PersonBuilder("Joe Shmo").Build();
    //     var chapter = new ChapterBuilder("1").Build();
    //
    //     var series = new SeriesBuilder("Test 1")
    //         .WithVolume(new VolumeBuilder("1")
    //             .WithChapter(new ChapterBuilder("1")
    //                 .WithPerson(person, PersonRole.Writer) // Assign person as Writer
    //                 .Build())
    //             .Build())
    //         .Build();
    //
    //     _unitOfWork.SeriesRepository.Add(series);
    //     await _unitOfWork.CommitAsync();
    //
    //     // Add same person as Editor
    //     await PersonHelper.UpdateChapterPeopleAsync(chapter, new List<string> { "Joe Shmo" }, PersonRole.Editor, _unitOfWork);
    //
    //     // Ensure that the same person is assigned with two roles
    //     var chapterPeople = chapter.People.Where(cp => cp.Person.Name == "Joe Shmo").ToList();
    //     Assert.Equal(2, chapterPeople.Count); // One for each role
    //     Assert.Contains(chapterPeople, cp => cp.Role == PersonRole.Writer);
    //     Assert.Contains(chapterPeople, cp => cp.Role == PersonRole.Editor);
    // }
}
