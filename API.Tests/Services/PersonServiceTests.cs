using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Person;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using Xunit;

namespace API.Tests.Services;

public class PersonServiceTests: AbstractDbTest
{

    [Fact]
    public async Task PersonMerge_KeepNonEmptyMetadata()
    {
        var ps = new PersonService(UnitOfWork);

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
            AniListId = 27,
        };

        UnitOfWork.PersonRepository.Attach(person1);
        UnitOfWork.PersonRepository.Attach(person2);
        await UnitOfWork.CommitAsync();

        await ps.MergePeopleAsync(person1, person2);

        var allPeople = await UnitOfWork.PersonRepository.GetAllPeople();
        Assert.Single(allPeople);

        var person = allPeople[0];
        Assert.Equal("Casey Delores", person.Name);
        Assert.NotEmpty(person.Description);
        Assert.Equal(27, person.AniListId);
        Assert.NotNull(person.HardcoverId);
        Assert.NotEmpty(person.HardcoverId);
        Assert.Contains(person.Aliases, pa => pa.Alias == "Delores Casey");
    }

    [Fact]
    public async Task PersonMerge_MergedPersonDestruction()
    {
        var ps = new PersonService(UnitOfWork);

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

        UnitOfWork.PersonRepository.Attach(person1);
        UnitOfWork.PersonRepository.Attach(person2);
        await UnitOfWork.CommitAsync();

        await ps.MergePeopleAsync(person1, person2);
        var allPeople = await UnitOfWork.PersonRepository.GetAllPeople();
        Assert.Single(allPeople);
    }

    [Fact]
    public async Task PersonMerge_RetentionChapters()
    {
        var ps = new PersonService(UnitOfWork);

        var library = new LibraryBuilder("My Library").Build();
        UnitOfWork.LibraryRepository.Add(library);
        await UnitOfWork.CommitAsync();

        var user = new AppUserBuilder("Amelia", "amelia@localhost")
            .WithLibrary(library).Build();
        UnitOfWork.UserRepository.Add(user);

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

        UnitOfWork.SeriesRepository.Add(series);
        UnitOfWork.SeriesRepository.Add(series2);
        await UnitOfWork.CommitAsync();

        await ps.MergePeopleAsync(person, person2);

        var allPeople = await UnitOfWork.PersonRepository.GetAllPeople();
        Assert.Single(allPeople);
        var mergedPerson = allPeople[0];

        Assert.Equal("Jillian Cowan", mergedPerson.Name);

        var chapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(1, 1, PersonRole.Editor);
        Assert.Equal(2, chapters.Count());

        chapter = await UnitOfWork.ChapterRepository.GetChapterAsync(1, ChapterIncludes.People);
        Assert.NotNull(chapter);
        Assert.Single(chapter.People);

        chapter2 = await UnitOfWork.ChapterRepository.GetChapterAsync(2, ChapterIncludes.People);
        Assert.NotNull(chapter2);
        Assert.Single(chapter2.People);

        Assert.Equal(chapter.People.First().PersonId, chapter2.People.First().PersonId);
    }

    [Fact]
    public async Task PersonAddAlias_NoOverlap()
    {
        await ResetDb();

        UnitOfWork.PersonRepository.Attach(new PersonBuilder("Jillian Cowan").Build());
        UnitOfWork.PersonRepository.Attach(new PersonBuilder("Jilly Cowan").WithAlias("Jolly Cowan").Build());
        await UnitOfWork.CommitAsync();

        var ps = new PersonService(UnitOfWork);

        var person1 = await UnitOfWork.PersonRepository.GetPersonByNameOrAliasAsync("Jillian Cowan");
        var person2 = await UnitOfWork.PersonRepository.GetPersonByNameOrAliasAsync("Jilly Cowan");
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
        Assert.True(success);

        Assert.Single(person2.Aliases);
    }

    protected override async Task ResetDb()
    {
        Context.Person.RemoveRange(Context.Person.ToList());

        await Context.SaveChangesAsync();
    }
}
