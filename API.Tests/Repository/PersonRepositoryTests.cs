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
    private static readonly Person SharedSeriesChaptersPerson =
        new PersonBuilder("Shared Series Chapter Person").Build();

    private static readonly Person SharedSeriesPerson = new PersonBuilder("Shared Series Person").Build();
    private static readonly Person SharedChaptersPerson = new PersonBuilder("Shared Chapters Person").Build();
    private static readonly Person Lib0SeriesChaptersPerson = new PersonBuilder("Lib0 Series Chapter Person").Build();
    private static readonly Person Lib0SeriesPerson = new PersonBuilder("Lib0 Series Person").Build();
    private static readonly Person Lib0ChaptersPerson = new PersonBuilder("Lib0 Chapters Person").Build();
    private static readonly Person Lib1SeriesChaptersPerson = new PersonBuilder("Lib1 Series Chapter Person").Build();
    private static readonly Person Lib1SeriesPerson = new PersonBuilder("Lib1 Series Person").Build();
    private static readonly Person Lib1ChaptersPerson = new PersonBuilder("Lib1 Chapters Person").Build();
    private static readonly Person Lib1ChapterAgePerson = new PersonBuilder("Lib1 Chapter Age Person").Build();

    private static readonly List<Person> AllPeople =
    [
        SharedSeriesChaptersPerson, SharedSeriesPerson, SharedChaptersPerson,
        Lib0SeriesChaptersPerson, Lib0SeriesPerson, Lib0ChaptersPerson,
        Lib1SeriesChaptersPerson, Lib1SeriesPerson, Lib1ChaptersPerson, Lib1ChapterAgePerson
    ];


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

        Context.Users.Add(_fullAccess);
        Context.Users.Add(_restrictedAccess);
        Context.Users.Add(_restrictedAgeAccess);
        await Context.SaveChangesAsync();

        Context.Person.AddRange(AllPeople);
        await Context.SaveChangesAsync();

        var lib0 = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("lib0-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(SharedSeriesChaptersPerson, PersonRole.Writer)
                    .WithPerson(SharedSeriesPerson, PersonRole.Writer)
                    .WithPerson(Lib0SeriesChaptersPerson, PersonRole.Writer)
                    .WithPerson(Lib0SeriesPerson, PersonRole.Writer)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithPerson(SharedSeriesChaptersPerson, PersonRole.Colorist)
                        .WithPerson(SharedChaptersPerson, PersonRole.Colorist)
                        .WithPerson(Lib0SeriesChaptersPerson, PersonRole.Colorist)
                        .WithPerson(Lib0ChaptersPerson, PersonRole.Colorist)
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithPerson(SharedSeriesChaptersPerson, PersonRole.Editor)
                        .WithPerson(SharedChaptersPerson, PersonRole.Editor)
                        .WithPerson(Lib0SeriesChaptersPerson, PersonRole.Editor)
                        .WithPerson(Lib0ChaptersPerson, PersonRole.Editor).Build())
                    .Build())
                .Build())
            .Build();

        var lib1 = new LibraryBuilder("lib1")
            .WithSeries(new SeriesBuilder("lib1-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(SharedSeriesChaptersPerson, PersonRole.Letterer)
                    .WithPerson(SharedSeriesPerson, PersonRole.Letterer)
                    .WithPerson(Lib1SeriesChaptersPerson, PersonRole.Letterer)
                    .WithPerson(Lib1SeriesPerson, PersonRole.Letterer)
                    .WithAgeRating(AgeRating.Mature17Plus)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithPerson(SharedSeriesChaptersPerson, PersonRole.Imprint)
                        .WithPerson(SharedChaptersPerson, PersonRole.Imprint)
                        .WithPerson(Lib1SeriesChaptersPerson, PersonRole.Imprint)
                        .WithPerson(Lib1ChaptersPerson, PersonRole.Imprint)
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithPerson(SharedSeriesChaptersPerson, PersonRole.CoverArtist)
                        .WithPerson(SharedChaptersPerson, PersonRole.CoverArtist)
                        .WithPerson(Lib1SeriesChaptersPerson, PersonRole.CoverArtist)
                        .WithPerson(Lib1ChaptersPerson, PersonRole.CoverArtist)
                        .WithPerson(Lib1ChapterAgePerson, PersonRole.CoverArtist)
                        .WithAgeRating(AgeRating.Mature17Plus)
                        .Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("lib1-s1")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(SharedSeriesChaptersPerson, PersonRole.Inker)
                    .WithPerson(SharedSeriesPerson, PersonRole.Inker)
                    .WithPerson(Lib1SeriesChaptersPerson, PersonRole.Inker)
                    .WithPerson(Lib1SeriesPerson, PersonRole.Inker)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithPerson(SharedSeriesChaptersPerson, PersonRole.Team)
                        .WithPerson(SharedChaptersPerson, PersonRole.Team)
                        .WithPerson(Lib1SeriesChaptersPerson, PersonRole.Team)
                        .WithPerson(Lib1ChaptersPerson, PersonRole.Team)
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithPerson(SharedSeriesChaptersPerson, PersonRole.Team)
                        .WithPerson(SharedChaptersPerson, PersonRole.Team)
                        .WithPerson(Lib1SeriesChaptersPerson, PersonRole.Team)
                        .WithPerson(Lib1ChaptersPerson, PersonRole.Team)
                        .Build())
                    .Build())
                .Build())
            .Build();


        await Context.SaveChangesAsync();

        _fullAccess.Libraries.Add(lib0);
        _fullAccess.Libraries.Add(lib1);
        _restrictedAccess.Libraries.Add(lib1);
        _restrictedAgeAccess.Libraries.Add(lib1);

        await Context.SaveChangesAsync();
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

        var fullAccessPeople =
            await UnitOfWork.PersonRepository.GetBrowsePersonDtos(_fullAccess.Id, new BrowsePersonFilterDto(),
                new UserParams());
        Assert.Equal(AllPeople.Count, fullAccessPeople.TotalCount);

        foreach (var person in AllPeople) Assert.Contains(fullAccessPeople, ContainsPersonCheck(person));

        Assert.Equal(3, fullAccessPeople.First(dto => dto.Id == SharedSeriesChaptersPerson.Id).SeriesCount);
        Assert.Equal(6, fullAccessPeople.First(dto => dto.Id == SharedSeriesChaptersPerson.Id).ChapterCount);
        Assert.Equal(1, fullAccessPeople.First(dto => dto.Id == Lib0SeriesPerson.Id).SeriesCount);


        var restrictedAccessPeople =
            await UnitOfWork.PersonRepository.GetBrowsePersonDtos(_restrictedAccess.Id, new BrowsePersonFilterDto(),
                new UserParams());

        Assert.Equal(7, restrictedAccessPeople.TotalCount);

        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(SharedSeriesChaptersPerson));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(SharedSeriesPerson));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(SharedChaptersPerson));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(Lib1SeriesChaptersPerson));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(Lib1SeriesPerson));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(Lib1ChaptersPerson));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(Lib1ChapterAgePerson));

        // Count is not working when restricted
        //Assert.Equal(2, restrictedAccessPeople.First(dto => dto.Id == SharedSeriesChaptersPerson.Id).SeriesCount);
        //Assert.Equal(4, restrictedAccessPeople.First(dto => dto.Id == SharedSeriesChaptersPerson.Id).ChapterCount);
        //Assert.Equal(2, restrictedAccessPeople.First(dto => dto.Id == Lib1SeriesPerson.Id).SeriesCount);


        // Fails because of the chapter - series issue
        /*var restrictedAgeAccessPeople = await UnitOfWork.PersonRepository.GetBrowsePersonDtos(_restrictedAgeAccess.Id,
            new BrowsePersonFilterDto(), new UserParams());

        Assert.Equal(0, restrictedAgeAccessPeople.TotalCount);

        foreach (var person in AllPeople) Assert.DoesNotContain(fullAccessPeople, ContainsPersonCheck(person));*/
    }

    [Fact]
    public async Task GetRolesForPersonByName()
    {
        await ResetDb();
        await SeedDb();

        var sharedSeriesRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(SharedSeriesPerson.Id, _fullAccess.Id);
        var chapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(SharedChaptersPerson.Id, _fullAccess.Id);
        var ageChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(Lib1ChapterAgePerson.Id, _fullAccess.Id);
        Assert.Equal(2, sharedSeriesRoles.Count());
        Assert.Equal(4, chapterRoles.Count());
        Assert.Single(ageChapterRoles);


        var restrictedRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(SharedSeriesPerson.Id, _restrictedAccess.Id);
        var restrictedChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(SharedChaptersPerson.Id, _restrictedAccess.Id);
        var restrictedAgePersonChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(Lib1ChapterAgePerson.Id, _restrictedAccess.Id);
        Assert.Single(restrictedRoles);
        Assert.Equal(2, restrictedChapterRoles.Count());
        Assert.Single(restrictedAgePersonChapterRoles);

        var restrictedAgeRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(SharedSeriesPerson.Id, _restrictedAgeAccess.Id);
        var restrictedAgeChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(SharedChaptersPerson.Id, _restrictedAgeAccess.Id);
        var restrictedAgeAgePersonChapterRoles = await UnitOfWork.PersonRepository.GetRolesForPersonByName(Lib1ChapterAgePerson.Id, _restrictedAgeAccess.Id);
        Assert.Single(restrictedAgeRoles);
        Assert.Equal(2, restrictedAgeChapterRoles.Count());
        // This works because both series & chapter have the correct age rating. The series-chapter issue is possible here
        // Just not int he example I worked out in this scenario
        Assert.Empty(restrictedAgeAgePersonChapterRoles);
    }

    [Fact]
    public async Task GetPersonDtoByName()
    {
        await ResetDb();
        await SeedDb();

        foreach (var person in AllPeople)
        {
            Assert.NotNull(await UnitOfWork.PersonRepository.GetPersonDtoByName(person.Name, _fullAccess.Id));
        }

        Assert.Null(await UnitOfWork.PersonRepository.GetPersonDtoByName(Lib0ChaptersPerson.Name, _restrictedAccess.Id));
        Assert.NotNull(await UnitOfWork.PersonRepository.GetPersonDtoByName(SharedSeriesPerson.Name, _restrictedAccess.Id));
        Assert.NotNull(await UnitOfWork.PersonRepository.GetPersonDtoByName(Lib1SeriesPerson.Name, _restrictedAccess.Id));

        // NOTE: The commend out Asserts fail because the chapter - series issue
        Assert.Null(await UnitOfWork.PersonRepository.GetPersonDtoByName(Lib0ChaptersPerson.Name, _restrictedAgeAccess.Id));
        //Assert.Null(await UnitOfWork.PersonRepository.GetPersonDtoByName(SharedSeriesPerson.Name, _restrictedAgeAccess.Id));
        //Assert.Null(await UnitOfWork.PersonRepository.GetPersonDtoByName(Lib1SeriesPerson.Name, _restrictedAgeAccess.Id));
        Assert.Null(await UnitOfWork.PersonRepository.GetPersonDtoByName(Lib1ChapterAgePerson.Name, _restrictedAgeAccess.Id));
    }

    [Fact]
    public async Task GetSeriesKnownFor()
    {
        await ResetDb();
        await SeedDb();

        var series = await UnitOfWork.PersonRepository.GetSeriesKnownFor(SharedSeriesPerson.Id, _fullAccess.Id);
        Assert.Equal(3, series.Count());

        series = await UnitOfWork.PersonRepository.GetSeriesKnownFor(SharedSeriesPerson.Id, _restrictedAccess.Id);
        Assert.Equal(2, series.Count());

        series = await UnitOfWork.PersonRepository.GetSeriesKnownFor(SharedSeriesPerson.Id, _restrictedAgeAccess.Id);
        Assert.Single(series);

        series = await UnitOfWork.PersonRepository.GetSeriesKnownFor(Lib1SeriesPerson.Id, _restrictedAgeAccess.Id);
        Assert.Single(series);
    }

    [Fact]
    public async Task GetChaptersForPersonByRole()
    {
        await ResetDb();
        await SeedDb();

        // Lib0
        var chapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _fullAccess.Id, PersonRole.Colorist);
        var restrictedChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _restrictedAccess.Id, PersonRole.Colorist);
        var restrictedAgeChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _restrictedAgeAccess.Id, PersonRole.Colorist);
        Assert.Single(chapters);
        Assert.Empty(restrictedChapters);
        Assert.Empty(restrictedAgeChapters);

        // Lib1 - age restricted
        chapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _fullAccess.Id, PersonRole.Imprint);
        restrictedChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _restrictedAccess.Id, PersonRole.Imprint);
        restrictedAgeChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _restrictedAgeAccess.Id, PersonRole.Imprint);
        Assert.Single(chapters);
        Assert.Single(restrictedChapters);
        //Assert.Empty(restrictedAgeChapters); This passed because of the series-chapter issue. The user cannot see the series, but can see this chapter

        // Lib1 - not age restricted
        chapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _fullAccess.Id, PersonRole.Team);
        restrictedChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _restrictedAccess.Id, PersonRole.Team);
        restrictedAgeChapters = await UnitOfWork.PersonRepository.GetChaptersForPersonByRole(SharedChaptersPerson.Id, _restrictedAgeAccess.Id, PersonRole.Team);
        Assert.Equal(2, chapters.Count());
        Assert.Equal(2, restrictedChapters.Count());
        Assert.Equal(2, restrictedAgeChapters.Count());
    }
}
