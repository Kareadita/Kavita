using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities.Enums;
using API.Entities.Person;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class PersonServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{

    [Fact]
    public async Task PersonMerge_KeepNonEmptyMetadata()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var ps = new PersonService(unitOfWork);

        var person1 = new Person
        {
            Name = "Casey Delores",
            NormalizedName = "Casey Delores".ToNormalized(),
            HardcoverId = "ANonEmptyId",
            MalId = 12,
        };

        var person2 = new Person
        {
            Name= "Delores Casey",
            NormalizedName = "Delores Casey".ToNormalized(),
            Description = "Hi, I'm Delores Casey!",
            Aliases = [new PersonAliasBuilder("Casey, Delores").Build()],
            AniListId = 27,
        };

        unitOfWork.PersonRepository.Attach(person1);
        unitOfWork.PersonRepository.Attach(person2);
        await unitOfWork.CommitAsync();

        await ps.MergePeopleAsync(person2, person1);

        var allPeople = await unitOfWork.PersonRepository.GetAllPeople();
        Assert.Single(allPeople);

        var person = allPeople[0];
        Assert.Equal("Casey Delores", person.Name);
        Assert.NotEmpty(person.Description);
        Assert.Equal(27, person.AniListId);
        Assert.NotNull(person.HardcoverId);
        Assert.NotEmpty(person.HardcoverId);
        Assert.Contains(person.Aliases, pa => pa.Alias == "Delores Casey");
        Assert.Contains(person.Aliases, pa => pa.Alias == "Casey, Delores");
    }

    [Fact]
    public async Task PersonMerge_MergedPersonDestruction()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var ps = new PersonService(unitOfWork);

        var person1 = new Person
        {
            Name = "Casey Delores",
            NormalizedName = "Casey Delores".ToNormalized(),
        };

        var person2 = new Person
        {
            Name = "Delores Casey",
            NormalizedName = "Delores Casey".ToNormalized(),
        };

        unitOfWork.PersonRepository.Attach(person1);
        unitOfWork.PersonRepository.Attach(person2);
        await unitOfWork.CommitAsync();

        await ps.MergePeopleAsync(person2, person1);
        var allPeople = await unitOfWork.PersonRepository.GetAllPeople();
        Assert.Single(allPeople);
    }

    [Fact]
    public async Task PersonMerge_RetentionChapters()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var ps = new PersonService(unitOfWork);

        var library = new LibraryBuilder("My Library").Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var user = new AppUserBuilder("Amelia", "amelia@localhost")
            .WithLibrary(library).Build();
        unitOfWork.UserRepository.Add(user);

        var person = new PersonBuilder("Jillian Cowan").Build();

        var person2 = new PersonBuilder("Cowan Jillian").Build();

        var chapter = new ChapterBuilder("1")
            .WithPerson(person, PersonRole.Editor)
            .Build();

        var chapter2 = new ChapterBuilder("2")
            .WithPerson(person2, PersonRole.Editor)
            .Build();

        var series = new SeriesBuilder("Test 1")
            .WithLibraryId(library.Id)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(chapter)
                .Build())
            .Build();

        var series2 = new SeriesBuilder("Test 2")
            .WithLibraryId(library.Id)
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(chapter2)
                .Build())
            .Build();

        unitOfWork.SeriesRepository.Add(series);
        unitOfWork.SeriesRepository.Add(series2);
        await unitOfWork.CommitAsync();

        await ps.MergePeopleAsync(person2, person);

        var allPeople = await unitOfWork.PersonRepository.GetAllPeople();
        Assert.Single(allPeople);
        var mergedPerson = allPeople[0];

        Assert.Equal("Jillian Cowan", mergedPerson.Name);

        var chapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(1, 1, PersonRole.Editor);
        Assert.Equal(2, chapters.Count());

        chapter = await unitOfWork.ChapterRepository.GetChapterAsync(1, ChapterIncludes.People);
        Assert.NotNull(chapter);
        Assert.Single(chapter.People);

        chapter2 = await unitOfWork.ChapterRepository.GetChapterAsync(2, ChapterIncludes.People);
        Assert.NotNull(chapter2);
        Assert.Single(chapter2.People);

        Assert.Equal(chapter.People.First().PersonId, chapter2.People.First().PersonId);
    }

    [Fact]
    public async Task PersonMerge_NoDuplicateChaptersOrSeries()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var ps = new PersonService(unitOfWork);

        var library = new LibraryBuilder("My Library").Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var user = new AppUserBuilder("Amelia", "amelia@localhost")
            .WithLibrary(library).Build();
        unitOfWork.UserRepository.Add(user);

        var person = new PersonBuilder("Jillian Cowan").Build();

        var person2 = new PersonBuilder("Cowan Jillian").Build();

        var chapter = new ChapterBuilder("1")
            .WithPerson(person, PersonRole.Editor)
            .WithPerson(person2, PersonRole.Colorist)
            .Build();

        var chapter2 = new ChapterBuilder("2")
            .WithPerson(person2, PersonRole.Editor)
            .WithPerson(person, PersonRole.Editor)
            .Build();

        var series = new SeriesBuilder("Test 1")
            .WithLibraryId(library.Id)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(chapter)
                .Build())
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(person, PersonRole.Editor)
                .WithPerson(person2, PersonRole.Editor)
                .Build())
            .Build();

        var series2 = new SeriesBuilder("Test 2")
            .WithLibraryId(library.Id)
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(chapter2)
                .Build())
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(person, PersonRole.Editor)
                .WithPerson(person2, PersonRole.Colorist)
                .Build())
            .Build();

        unitOfWork.SeriesRepository.Add(series);
        unitOfWork.SeriesRepository.Add(series2);
        await unitOfWork.CommitAsync();

        await ps.MergePeopleAsync(person2, person);
        var allPeople = await unitOfWork.PersonRepository.GetAllPeople();
        Assert.Single(allPeople);

        var mergedPerson = await unitOfWork.PersonRepository.GetPersonById(person.Id, PersonIncludes.All);
        Assert.NotNull(mergedPerson);
        Assert.Equal(3, mergedPerson.ChapterPeople.Count);
        Assert.Equal(3, mergedPerson.SeriesMetadataPeople.Count);

        chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapter.Id, ChapterIncludes.People);
        Assert.NotNull(chapter);
        Assert.Equal(2, chapter.People.Count);
        Assert.Single(chapter.People.Select(p => p.Person.Id).Distinct());
        Assert.Contains(chapter.People, p => p.Role == PersonRole.Editor);
        Assert.Contains(chapter.People, p => p.Role == PersonRole.Colorist);

        chapter2 = await unitOfWork.ChapterRepository.GetChapterAsync(chapter2.Id, ChapterIncludes.People);
        Assert.NotNull(chapter2);
        Assert.Single(chapter2.People);
        Assert.Contains(chapter2.People, p => p.Role == PersonRole.Editor);
        Assert.DoesNotContain(chapter2.People, p => p.Role == PersonRole.Colorist);

        series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(series.Id, SeriesIncludes.Metadata);
        Assert.NotNull(series);
        Assert.Single(series.Metadata.People);
        Assert.Contains(series.Metadata.People, p => p.Role == PersonRole.Editor);
        Assert.DoesNotContain(series.Metadata.People, p => p.Role == PersonRole.Colorist);

        series2 = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(series2.Id, SeriesIncludes.Metadata);
        Assert.NotNull(series2);
        Assert.Equal(2, series2.Metadata.People.Count);
        Assert.Contains(series2.Metadata.People, p => p.Role == PersonRole.Editor);
        Assert.Contains(series2.Metadata.People, p => p.Role == PersonRole.Colorist);


    }

    [Fact]
    public async Task PersonAddAlias_NoOverlap()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        unitOfWork.PersonRepository.Attach(new PersonBuilder("Jillian Cowan").Build());
        unitOfWork.PersonRepository.Attach(new PersonBuilder("Jilly Cowan").WithAlias("Jolly Cowan").Build());
        await unitOfWork.CommitAsync();

        var ps = new PersonService(unitOfWork);

        var person1 = await unitOfWork.PersonRepository.GetPersonByNameOrAliasAsync("Jillian Cowan");
        var person2 = await unitOfWork.PersonRepository.GetPersonByNameOrAliasAsync("Jilly Cowan");
        Assert.NotNull(person1);
        Assert.NotNull(person2);

        // Overlap on Name
        var success = await ps.UpdatePersonAliasesAsync(person1, ["Jilly Cowan"]);
        Assert.False(success);

        // Overlap on alias
        success = await ps.UpdatePersonAliasesAsync(person1, ["Jolly Cowan"]);
        Assert.False(success);

        // No overlap
        success = await ps.UpdatePersonAliasesAsync(person2, ["Jilly Joy Cowan"]);
        Assert.True(success);

        // Some overlap
        success = await ps.UpdatePersonAliasesAsync(person1, ["Jolly Cowan", "Jilly Joy Cowan"]);
        Assert.False(success);

        // Some overlap
        success = await ps.UpdatePersonAliasesAsync(person1, ["Jolly Cowan", "Jilly Joy Cowan"]);
        Assert.False(success);

        Assert.Single(person2.Aliases);
    }
}
