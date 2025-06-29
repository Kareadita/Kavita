using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Metadata.Browse;
using API.DTOs.Metadata.Browse.Requests;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Person;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Repository;

public class PersonRepositoryTests : AbstractDbTest
{
    private AppUser _fullAccess;
    private AppUser _restrictedAccess;
    private AppUser _restrictedAgeAccess;

    protected override async Task ResetDb()
    {
        Context.Person.RemoveRange(Context.Person.ToList());
        Context.Library.RemoveRange(Context.Library.ToList());
        Context.AppUser.RemoveRange(Context.AppUser.ToList());
        await UnitOfWork.CommitAsync();
    }

    private async Task SeedDb()
    {
        _fullAccess = new AppUserBuilder("amelia", "amelia@example.com").Build();
        _restrictedAccess = new AppUserBuilder("mila", "mila@example.com").Build();
        _restrictedAgeAccess = new AppUserBuilder("eva", "eva@example.com").Build();
        _restrictedAgeAccess.AgeRestriction = AgeRating.Teen;
        _restrictedAgeAccess.AgeRestrictionIncludeUnknowns = true;

        Context.AppUser.Add(_fullAccess);
        Context.AppUser.Add(_restrictedAccess);
        Context.AppUser.Add(_restrictedAgeAccess);
        await Context.SaveChangesAsync();

        var people = CreateTestPeople();
        Context.Person.AddRange(people);
        await Context.SaveChangesAsync();

        var libraries = CreateTestLibraries(people);
        Context.Library.AddRange(libraries);
        await Context.SaveChangesAsync();

        _fullAccess.Libraries.Add(libraries[0]); // lib0
        _fullAccess.Libraries.Add(libraries[1]); // lib1
        _restrictedAccess.Libraries.Add(libraries[1]); // lib1 only
        _restrictedAgeAccess.Libraries.Add(libraries[1]); // lib1 only

        await Context.SaveChangesAsync();
    }

    private static List<Person> CreateTestPeople()
    {
        return new List<Person>
        {
            new PersonBuilder("Shared Series Chapter Person").Build(),
            new PersonBuilder("Shared Series Person").Build(),
            new PersonBuilder("Shared Chapters Person").Build(),
            new PersonBuilder("Lib0 Series Chapter Person").Build(),
            new PersonBuilder("Lib0 Series Person").Build(),
            new PersonBuilder("Lib0 Chapters Person").Build(),
            new PersonBuilder("Lib1 Series Chapter Person").Build(),
            new PersonBuilder("Lib1 Series Person").Build(),
            new PersonBuilder("Lib1 Chapters Person").Build(),
            new PersonBuilder("Lib1 Chapter Age Person").Build()
        };
    }

    private static List<Library> CreateTestLibraries(List<Person> people)
    {
        var lib0 = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("lib0-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.Writer)
                    .WithPerson(GetPersonByName(people, "Shared Series Person"), PersonRole.Writer)
                    .WithPerson(GetPersonByName(people, "Lib0 Series Chapter Person"), PersonRole.Writer)
                    .WithPerson(GetPersonByName(people, "Lib0 Series Person"), PersonRole.Writer)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.Colorist)
                        .WithPerson(GetPersonByName(people, "Shared Chapters Person"), PersonRole.Colorist)
                        .WithPerson(GetPersonByName(people, "Lib0 Series Chapter Person"), PersonRole.Colorist)
                        .WithPerson(GetPersonByName(people, "Lib0 Chapters Person"), PersonRole.Colorist)
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.Editor)
                        .WithPerson(GetPersonByName(people, "Shared Chapters Person"), PersonRole.Editor)
                        .WithPerson(GetPersonByName(people, "Lib0 Series Chapter Person"), PersonRole.Editor)
                        .WithPerson(GetPersonByName(people, "Lib0 Chapters Person"), PersonRole.Editor)
                        .Build())
                    .Build())
                .Build())
            .Build();

        var lib1 = new LibraryBuilder("lib1")
            .WithSeries(new SeriesBuilder("lib1-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.Letterer)
                    .WithPerson(GetPersonByName(people, "Shared Series Person"), PersonRole.Letterer)
                    .WithPerson(GetPersonByName(people, "Lib1 Series Chapter Person"), PersonRole.Letterer)
                    .WithPerson(GetPersonByName(people, "Lib1 Series Person"), PersonRole.Letterer)
                    .WithAgeRating(AgeRating.Mature17Plus)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.Imprint)
                        .WithPerson(GetPersonByName(people, "Shared Chapters Person"), PersonRole.Imprint)
                        .WithPerson(GetPersonByName(people, "Lib1 Series Chapter Person"), PersonRole.Imprint)
                        .WithPerson(GetPersonByName(people, "Lib1 Chapters Person"), PersonRole.Imprint)
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.CoverArtist)
                        .WithPerson(GetPersonByName(people, "Shared Chapters Person"), PersonRole.CoverArtist)
                        .WithPerson(GetPersonByName(people, "Lib1 Series Chapter Person"), PersonRole.CoverArtist)
                        .WithPerson(GetPersonByName(people, "Lib1 Chapters Person"), PersonRole.CoverArtist)
                        .WithPerson(GetPersonByName(people, "Lib1 Chapter Age Person"), PersonRole.CoverArtist)
                        .WithAgeRating(AgeRating.Mature17Plus)
                        .Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("lib1-s1")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.Inker)
                    .WithPerson(GetPersonByName(people, "Shared Series Person"), PersonRole.Inker)
                    .WithPerson(GetPersonByName(people, "Lib1 Series Chapter Person"), PersonRole.Inker)
                    .WithPerson(GetPersonByName(people, "Lib1 Series Person"), PersonRole.Inker)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.Team)
                        .WithPerson(GetPersonByName(people, "Shared Chapters Person"), PersonRole.Team)
                        .WithPerson(GetPersonByName(people, "Lib1 Series Chapter Person"), PersonRole.Team)
                        .WithPerson(GetPersonByName(people, "Lib1 Chapters Person"), PersonRole.Team)
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithPerson(GetPersonByName(people, "Shared Series Chapter Person"), PersonRole.Translator)
                        .WithPerson(GetPersonByName(people, "Shared Chapters Person"), PersonRole.Translator)
                        .WithPerson(GetPersonByName(people, "Lib1 Series Chapter Person"), PersonRole.Translator)
                        .WithPerson(GetPersonByName(people, "Lib1 Chapters Person"), PersonRole.Translator)
                        .Build())
                    .Build())
                .Build())
            .Build();

        return new List<Library> { lib0, lib1 };
    }

    private static Person GetPersonByName(List<Person> people, string name)
    {
        return people.First(p => p.Name == name);
    }

    private Person GetPersonByName(string name)
    {
        return Context.Person.First(p => p.Name == name);
    }

    private static Predicate<BrowsePersonDto> ContainsPersonCheck(Person person)
    {
        return p => p.Id == person.Id;
    }

    [Fact]
    public async Task GetBrowsePersonDtos()
    {
        await ResetDb();
        await SeedDb();

        // Get people from database for assertions
        var sharedSeriesChaptersPerson = GetPersonByName("Shared Series Chapter Person");
        var lib0SeriesPerson = GetPersonByName("Lib0 Series Person");
        var lib1SeriesPerson = GetPersonByName("Lib1 Series Person");
        var lib1ChapterAgePerson = GetPersonByName("Lib1 Chapter Age Person");
        var allPeople = Context.Person.ToList();

        var fullAccessPeople =
            await UnitOfWork.PersonRepository.GetBrowsePersonDtos(_fullAccess.Id, new BrowsePersonFilterDto(),
                new UserParams());
        Assert.Equal(allPeople.Count, fullAccessPeople.TotalCount);

        foreach (var person in allPeople)
            Assert.Contains(fullAccessPeople, ContainsPersonCheck(person));

        // 1 series in lib0, 2 series in lib1
        Assert.Equal(3, fullAccessPeople.First(dto => dto.Id == sharedSeriesChaptersPerson.Id).SeriesCount);
        // 3 series with each 2 chapters
        Assert.Equal(6, fullAccessPeople.First(dto => dto.Id == sharedSeriesChaptersPerson.Id).ChapterCount);
        // 1 series in lib0
        Assert.Equal(1, fullAccessPeople.First(dto => dto.Id == lib0SeriesPerson.Id).SeriesCount);
        // 2 series in lib1
        Assert.Equal(2, fullAccessPeople.First(dto => dto.Id == lib1SeriesPerson.Id).SeriesCount);

        var restrictedAccessPeople =
            await UnitOfWork.PersonRepository.GetBrowsePersonDtos(_restrictedAccess.Id, new BrowsePersonFilterDto(),
                new UserParams());

        Assert.Equal(7, restrictedAccessPeople.TotalCount);

        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName("Shared Series Chapter Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName("Shared Series Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName("Shared Chapters Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName("Lib1 Series Chapter Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName("Lib1 Series Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName("Lib1 Chapters Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName("Lib1 Chapter Age Person")));

        // 2 series in lib1, no series in lib0
        Assert.Equal(2, restrictedAccessPeople.First(dto => dto.Id == sharedSeriesChaptersPerson.Id).SeriesCount);
        // 2 series with each 2 chapters
        Assert.Equal(4, restrictedAccessPeople.First(dto => dto.Id == sharedSeriesChaptersPerson.Id).ChapterCount);
        // 2 series in lib1
        Assert.Equal(2, restrictedAccessPeople.First(dto => dto.Id == lib1SeriesPerson.Id).SeriesCount);

        var restrictedAgeAccessPeople = await UnitOfWork.PersonRepository.GetBrowsePersonDtos(_restrictedAgeAccess.Id,
            new BrowsePersonFilterDto(), new UserParams());

        // Note: There is a potential bug here where a person in a different chapter of an age restricted series will show up
        Assert.Equal(6, restrictedAgeAccessPeople.TotalCount);

        // No access to the age restricted chapter
        Assert.DoesNotContain(restrictedAgeAccessPeople, ContainsPersonCheck(lib1ChapterAgePerson));
    }

    [Fact]
    public async Task GetRolesForPersonByName()
    {
        await ResetDb();
        await SeedDb();

        var sharedSeriesPerson = GetPersonByName("Shared Series Person");
        var sharedChaptersPerson = GetPersonByName("Shared Chapters Person");
        var lib1ChapterAgePerson = GetPersonByName("Lib1 Chapter Age Person");

        var sharedSeriesRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(sharedSeriesPerson.Id, _fullAccess.Id);
        var chapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(sharedChaptersPerson.Id, _fullAccess.Id);
        var ageChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(lib1ChapterAgePerson.Id, _fullAccess.Id);
        Assert.Equal(3, sharedSeriesRoles.Count());
        Assert.Equal(6, chapterRoles.Count());
        Assert.Single(ageChapterRoles);

        var restrictedRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(sharedSeriesPerson.Id, _restrictedAccess.Id);
        var restrictedChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(sharedChaptersPerson.Id, _restrictedAccess.Id);
        var restrictedAgePersonChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(lib1ChapterAgePerson.Id, _restrictedAccess.Id);
        Assert.Equal(2, restrictedRoles.Count());
        Assert.Equal(4, restrictedChapterRoles.Count());
        Assert.Single(restrictedAgePersonChapterRoles);

        var restrictedAgeRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(sharedSeriesPerson.Id, _restrictedAgeAccess.Id);
        var restrictedAgeChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(sharedChaptersPerson.Id, _restrictedAgeAccess.Id);
        var restrictedAgeAgePersonChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(lib1ChapterAgePerson.Id, _restrictedAgeAccess.Id);
        Assert.Single(restrictedAgeRoles);
        Assert.Equal(2, restrictedAgeChapterRoles.Count());
        // Note: There is a potential bug here where a person in a different chapter of an age restricted series will show up
        Assert.Empty(restrictedAgeAgePersonChapterRoles);
    }

    [Fact]
    public async Task GetPersonDtoByName()
    {
        await ResetDb();
        await SeedDb();

        var allPeople = Context.Person.ToList();

        foreach (var person in allPeople)
        {
            Assert.NotNull(await UnitOfWork.PersonRepository.GetPersonDtoByName(person.Name, _fullAccess.Id));
        }

        Assert.Null(await UnitOfWork.PersonRepository.GetPersonDtoByName("Lib0 Chapters Person", _restrictedAccess.Id));
        Assert.NotNull(await UnitOfWork.PersonRepository.GetPersonDtoByName("Shared Series Person", _restrictedAccess.Id));
        Assert.NotNull(await UnitOfWork.PersonRepository.GetPersonDtoByName("Lib1 Series Person", _restrictedAccess.Id));

        Assert.Null(await UnitOfWork.PersonRepository.GetPersonDtoByName("Lib0 Chapters Person", _restrictedAgeAccess.Id));
        Assert.NotNull(await UnitOfWork.PersonRepository.GetPersonDtoByName("Lib1 Series Person", _restrictedAgeAccess.Id));
        // Note: There is a potential bug here where a person in a different chapter of an age restricted series will show up
        Assert.Null(await UnitOfWork.PersonRepository.GetPersonDtoByName("Lib1 Chapter Age Person", _restrictedAgeAccess.Id));
    }

    [Fact]
    public async Task GetSeriesKnownFor()
    {
        await ResetDb();
        await SeedDb();

        var sharedSeriesPerson = GetPersonByName("Shared Series Person");
        var lib1SeriesPerson = GetPersonByName("Lib1 Series Person");

        var series = await UnitOfWork.PersonRepository.GetSeriesKnownFor(sharedSeriesPerson.Id, _fullAccess.Id);
        Assert.Equal(3, series.Count());

        series = await UnitOfWork.PersonRepository.GetSeriesKnownFor(sharedSeriesPerson.Id, _restrictedAccess.Id);
        Assert.Equal(2, series.Count());

        series = await UnitOfWork.PersonRepository.GetSeriesKnownFor(sharedSeriesPerson.Id, _restrictedAgeAccess.Id);
        Assert.Single(series);

        series = await UnitOfWork.PersonRepository.GetSeriesKnownFor(lib1SeriesPerson.Id, _restrictedAgeAccess.Id);
        Assert.Single(series);
    }

    [Fact]
    public async Task GetChaptersForPersonByRole()
    {
        await ResetDb();
        await SeedDb();

        var sharedChaptersPerson = GetPersonByName("Shared Chapters Person");

        // Lib0
        var chapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _fullAccess.Id, PersonRole.Colorist);
        var restrictedChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _restrictedAccess.Id, PersonRole.Colorist);
        var restrictedAgeChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _restrictedAgeAccess.Id, PersonRole.Colorist);
        Assert.Single(chapters);
        Assert.Empty(restrictedChapters);
        Assert.Empty(restrictedAgeChapters);

        // Lib1 - age restricted series
        chapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _fullAccess.Id, PersonRole.Imprint);
        restrictedChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _restrictedAccess.Id, PersonRole.Imprint);
        restrictedAgeChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _restrictedAgeAccess.Id, PersonRole.Imprint);
        Assert.Single(chapters);
        Assert.Single(restrictedChapters);
        Assert.Empty(restrictedAgeChapters);

        // Lib1 - not age restricted series
        chapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _fullAccess.Id, PersonRole.Team);
        restrictedChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _restrictedAccess.Id, PersonRole.Team);
        restrictedAgeChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, _restrictedAgeAccess.Id, PersonRole.Team);
        Assert.Single(chapters);
        Assert.Single(restrictedChapters);
        Assert.Single(restrictedAgeChapters);
    }
}
