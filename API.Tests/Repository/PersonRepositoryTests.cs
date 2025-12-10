using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Metadata.Browse;
using API.DTOs.Metadata.Browse.Requests;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Person;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Repository;

public class PersonRepositoryTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{

    private static async Task<(AppUser, AppUser, AppUser)> Setup(DataContext context)
    {
        var fullAccess = new AppUserBuilder("amelia", "amelia@example.com").Build();
        var restrictedAccess = new AppUserBuilder("mila", "mila@example.com").Build();
        var restrictedAgeAccess = new AppUserBuilder("eva", "eva@example.com").Build();
        restrictedAgeAccess.AgeRestriction = AgeRating.Teen;
        restrictedAgeAccess.AgeRestrictionIncludeUnknowns = true;

        context.AppUser.Add(fullAccess);
        context.AppUser.Add(restrictedAccess);
        context.AppUser.Add(restrictedAgeAccess);
        await context.SaveChangesAsync();

        var people = CreateTestPeople();
        context.Person.AddRange(people);
        await context.SaveChangesAsync();

        var libraries = CreateTestLibraries(context, people);
        context.Library.AddRange(libraries);
        await context.SaveChangesAsync();

        fullAccess.Libraries.Add(libraries[0]); // lib0
        fullAccess.Libraries.Add(libraries[1]); // lib1
        restrictedAccess.Libraries.Add(libraries[1]); // lib1 only
        restrictedAgeAccess.Libraries.Add(libraries[1]); // lib1 only

        await context.SaveChangesAsync();

        return (fullAccess, restrictedAccess, restrictedAgeAccess);
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

    private static List<Library> CreateTestLibraries(DataContext context, List<Person> people)
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

    private Person GetPersonByName(DataContext context, string name)
    {
        return context.Person.First(p => p.Name == name);
    }

    private static Predicate<BrowsePersonDto> ContainsPersonCheck(Person person)
    {
        return p => p.Id == person.Id;
    }

    [Fact]
    public async Task GetBrowsePersonDtos()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (fullAccess, restrictedAccess, restrictedAgeAccess) = await Setup(context);

        // Get people from database for assertions
        var sharedSeriesChaptersPerson = GetPersonByName(context, "Shared Series Chapter Person");
        var lib0SeriesPerson = GetPersonByName(context, "Lib0 Series Person");
        var lib1SeriesPerson = GetPersonByName(context, "Lib1 Series Person");
        var lib1ChapterAgePerson = GetPersonByName(context, "Lib1 Chapter Age Person");
        var allPeople = context.Person.ToList();

        var fullAccessPeople =
            await unitOfWork.PersonRepository.GetBrowsePersonDtos(fullAccess.Id, new BrowsePersonFilterDto(),
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
            await unitOfWork.PersonRepository.GetBrowsePersonDtos(restrictedAccess.Id, new BrowsePersonFilterDto(),
                new UserParams());

        Assert.Equal(7, restrictedAccessPeople.TotalCount);

        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName(context, "Shared Series Chapter Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName(context, "Shared Series Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName(context, "Shared Chapters Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName(context, "Lib1 Series Chapter Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName(context, "Lib1 Series Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName(context, "Lib1 Chapters Person")));
        Assert.Contains(restrictedAccessPeople, ContainsPersonCheck(GetPersonByName(context, "Lib1 Chapter Age Person")));

        // 2 series in lib1, no series in lib0
        Assert.Equal(2, restrictedAccessPeople.First(dto => dto.Id == sharedSeriesChaptersPerson.Id).SeriesCount);
        // 2 series with each 2 chapters
        Assert.Equal(4, restrictedAccessPeople.First(dto => dto.Id == sharedSeriesChaptersPerson.Id).ChapterCount);
        // 2 series in lib1
        Assert.Equal(2, restrictedAccessPeople.First(dto => dto.Id == lib1SeriesPerson.Id).SeriesCount);

        var restrictedAgeAccessPeople = await unitOfWork.PersonRepository.GetBrowsePersonDtos(restrictedAgeAccess.Id,
            new BrowsePersonFilterDto(), new UserParams());

        // Note: There is a potential bug here where a person in a different chapter of an age restricted series will show up
        Assert.Equal(6, restrictedAgeAccessPeople.TotalCount);

        // No access to the age restricted chapter
        Assert.DoesNotContain(restrictedAgeAccessPeople, ContainsPersonCheck(lib1ChapterAgePerson));
    }

    [Fact]
    public async Task GetRolesForPersonByName()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (fullAccess, restrictedAccess, restrictedAgeAccess) = await Setup(context);

        var sharedSeriesPerson = GetPersonByName(context, "Shared Series Person");
        var sharedChaptersPerson = GetPersonByName(context, "Shared Chapters Person");
        var lib1ChapterAgePerson = GetPersonByName(context, "Lib1 Chapter Age Person");

        var sharedSeriesRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(sharedSeriesPerson.Id, fullAccess.Id);
        var chapterRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(sharedChaptersPerson.Id, fullAccess.Id);
        var ageChapterRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(lib1ChapterAgePerson.Id, fullAccess.Id);
        Assert.Equal(3, sharedSeriesRoles.Count());
        Assert.Equal(6, chapterRoles.Count());
        Assert.Single(ageChapterRoles);

        var restrictedRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(sharedSeriesPerson.Id, restrictedAccess.Id);
        var restrictedChapterRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(sharedChaptersPerson.Id, restrictedAccess.Id);
        var restrictedAgePersonChapterRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(lib1ChapterAgePerson.Id, restrictedAccess.Id);
        Assert.Equal(2, restrictedRoles.Count());
        Assert.Equal(4, restrictedChapterRoles.Count());
        Assert.Single(restrictedAgePersonChapterRoles);

        var restrictedAgeRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(sharedSeriesPerson.Id, restrictedAgeAccess.Id);
        var restrictedAgeChapterRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(sharedChaptersPerson.Id, restrictedAgeAccess.Id);
        var restrictedAgeAgePersonChapterRoles = await unitOfWork.PersonRepository.GetRolesForPersonByName(lib1ChapterAgePerson.Id, restrictedAgeAccess.Id);
        Assert.Single(restrictedAgeRoles);
        Assert.Equal(2, restrictedAgeChapterRoles.Count());
        // Note: There is a potential bug here where a person in a different chapter of an age restricted series will show up
        Assert.Empty(restrictedAgeAgePersonChapterRoles);
    }

    [Fact]
    public async Task GetPersonDtoByName()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (fullAccess, restrictedAccess, restrictedAgeAccess) = await Setup(context);

        var allPeople = context.Person.ToList();

        foreach (var person in allPeople)
        {
            Assert.NotNull(await unitOfWork.PersonRepository.GetPersonDtoByName(person.Name, fullAccess.Id));
        }

        Assert.Null(await unitOfWork.PersonRepository.GetPersonDtoByName("Lib0 Chapters Person", restrictedAccess.Id));
        Assert.NotNull(await unitOfWork.PersonRepository.GetPersonDtoByName("Shared Series Person", restrictedAccess.Id));
        Assert.NotNull(await unitOfWork.PersonRepository.GetPersonDtoByName("Lib1 Series Person", restrictedAccess.Id));

        Assert.Null(await unitOfWork.PersonRepository.GetPersonDtoByName("Lib0 Chapters Person", restrictedAgeAccess.Id));
        Assert.NotNull(await unitOfWork.PersonRepository.GetPersonDtoByName("Lib1 Series Person", restrictedAgeAccess.Id));
        // Note: There is a potential bug here where a person in a different chapter of an age restricted series will show up
        Assert.Null(await unitOfWork.PersonRepository.GetPersonDtoByName("Lib1 Chapter Age Person", restrictedAgeAccess.Id));
    }

    [Fact]
    public async Task GetSeriesKnownFor()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (fullAccess, restrictedAccess, restrictedAgeAccess) = await Setup(context);

        var sharedSeriesPerson = GetPersonByName(context, "Shared Series Person");
        var lib1SeriesPerson = GetPersonByName(context, "Lib1 Series Person");

        var series = await unitOfWork.PersonRepository.GetSeriesKnownFor(sharedSeriesPerson.Id, fullAccess.Id);
        Assert.Equal(3, series.Count());

        series = await unitOfWork.PersonRepository.GetSeriesKnownFor(sharedSeriesPerson.Id, restrictedAccess.Id);
        Assert.Equal(2, series.Count());

        series = await unitOfWork.PersonRepository.GetSeriesKnownFor(sharedSeriesPerson.Id, restrictedAgeAccess.Id);
        Assert.Single(series);

        series = await unitOfWork.PersonRepository.GetSeriesKnownFor(lib1SeriesPerson.Id, restrictedAgeAccess.Id);
        Assert.Single(series);
    }

    [Fact]
    public async Task GetChaptersForPersonByRole()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (fullAccess, restrictedAccess, restrictedAgeAccess) = await Setup(context);

        var sharedChaptersPerson = GetPersonByName(context, "Shared Chapters Person");

        // Lib0
        var chapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, fullAccess.Id, PersonRole.Colorist);
        var restrictedChapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, restrictedAccess.Id, PersonRole.Colorist);
        var restrictedAgeChapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, restrictedAgeAccess.Id, PersonRole.Colorist);
        Assert.Single(chapters);
        Assert.Empty(restrictedChapters);
        Assert.Empty(restrictedAgeChapters);

        // Lib1 - age restricted series
        chapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, fullAccess.Id, PersonRole.Imprint);
        restrictedChapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, restrictedAccess.Id, PersonRole.Imprint);
        restrictedAgeChapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, restrictedAgeAccess.Id, PersonRole.Imprint);
        Assert.Single(chapters);
        Assert.Single(restrictedChapters);
        Assert.Empty(restrictedAgeChapters);

        // Lib1 - not age restricted series
        chapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, fullAccess.Id, PersonRole.Team);
        restrictedChapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, restrictedAccess.Id, PersonRole.Team);
        restrictedAgeChapters = await unitOfWork.PersonRepository.GetChaptersForPersonByRole(sharedChaptersPerson.Id, restrictedAgeAccess.Id, PersonRole.Team);
        Assert.Single(chapters);
        Assert.Single(restrictedChapters);
        Assert.Single(restrictedAgeChapters);
    }
}
