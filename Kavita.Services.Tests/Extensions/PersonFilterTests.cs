using System.Collections.Generic;
using System.Linq;
using Kavita.Common;
using Kavita.Database;
using Kavita.Database.Extensions.Filters;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Builders;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Extensions;

public class PersonFilterTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{

    #region HasPersonName

    private async Task SetupHasPersonName(DataContext context)
    {
        context.Person.AddRange(
            new PersonBuilder("John Smith").Build(),
            new PersonBuilder("Jane Doe").Build()
        );
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasPersonName_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonName(context);

        var result = await context.Person
            .HasPersonName(true, FilterComparison.Equal, "John Smith")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("John Smith", result[0].Name);
    }

    [Fact]
    public async Task HasPersonName_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonName(context);

        var result = await context.Person
            .HasPersonName(true, FilterComparison.NotEqual, "John Smith")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Jane Doe", result[0].Name);
    }

    [Fact]
    public async Task HasPersonName_BeginsWith_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonName(context);

        var result = await context.Person
            .HasPersonName(true, FilterComparison.BeginsWith, "John")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("John Smith", result[0].Name);
    }

    [Fact]
    public async Task HasPersonName_EndsWith_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonName(context);

        var result = await context.Person
            .HasPersonName(true, FilterComparison.EndsWith, "Doe")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Jane Doe", result[0].Name);
    }

    [Fact]
    public async Task HasPersonName_Matches_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonName(context);

        var result = await context.Person
            .HasPersonName(true, FilterComparison.Matches, "hn Sm")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("John Smith", result[0].Name);
    }

    [Fact]
    public async Task HasPersonName_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonName(context);

        var result = await context.Person
            .HasPersonName(false, FilterComparison.Equal, "John Smith")
            .ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task HasPersonName_EmptyString_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonName(context);

        var result = await context.Person
            .HasPersonName(true, FilterComparison.Equal, string.Empty)
            .ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task HasPersonName_UnsupportedComparison_ThrowsException()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonName(context);

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await context.Person.HasPersonName(true, FilterComparison.GreaterThan, "John").ToListAsync();
        });
    }

    #endregion

    #region HasPersonRole

    private async Task SetupHasPersonRole(DataContext context)
    {
        var writer = new PersonBuilder("Writer Person").Build();
        var artist = new PersonBuilder("Artist Person").Build();
        var both = new PersonBuilder("Both Person").Build();

        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(writer, PersonRole.Writer)
                    .WithPerson(both, PersonRole.Writer)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10)
                        .WithPerson(artist, PersonRole.CoverArtist)
                        .WithPerson(both, PersonRole.CoverArtist)
                        .Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(library);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasPersonRole_Contains_WriterRole_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonRole(context);

        var result = await context.Person
            .HasPersonRole(true, FilterComparison.Contains, new List<PersonRole> { PersonRole.Writer })
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Writer Person");
        Assert.Contains(result, p => p.Name == "Both Person");
    }

    [Fact]
    public async Task HasPersonRole_Contains_CoverArtistRole_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonRole(context);

        var result = await context.Person
            .HasPersonRole(true, FilterComparison.Contains, new List<PersonRole> { PersonRole.CoverArtist })
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Artist Person");
        Assert.Contains(result, p => p.Name == "Both Person");
    }

    [Fact]
    public async Task HasPersonRole_NotContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonRole(context);

        var result = await context.Person
            .HasPersonRole(true, FilterComparison.NotContains, new List<PersonRole> { PersonRole.Writer, PersonRole.CoverArtist })
            .ToListAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task HasPersonRole_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonRole(context);

        var result = await context.Person
            .HasPersonRole(false, FilterComparison.Contains, new List<PersonRole> { PersonRole.Writer })
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task HasPersonRole_EmptyRoles_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonRole(context);

        var result = await context.Person
            .HasPersonRole(true, FilterComparison.Contains, new List<PersonRole>())
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    #endregion

    #region HasPersonSeriesCount

    private async Task SetupHasPersonSeriesCount(DataContext context)
    {
        var personNone = new PersonBuilder("No Series Person").Build();
        var personOne = new PersonBuilder("One Series Person").Build();
        var personTwo = new PersonBuilder("Two Series Person").Build();

        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(personOne, PersonRole.Writer)
                    .WithPerson(personTwo, PersonRole.Writer)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("SeriesB").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithPerson(personTwo, PersonRole.Writer)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();

        context.Person.Add(personNone);
        context.Library.Add(library);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasPersonSeriesCount_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonSeriesCount(context);

        var result = await context.Person
            .HasPersonSeriesCount(true, FilterComparison.Equal, 2)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Two Series Person", result[0].Name);
    }

    [Fact]
    public async Task HasPersonSeriesCount_Equal_Zero_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonSeriesCount(context);

        var result = await context.Person
            .HasPersonSeriesCount(true, FilterComparison.Equal, 0)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("No Series Person", result[0].Name);
    }

    [Fact]
    public async Task HasPersonSeriesCount_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonSeriesCount(context);

        var result = await context.Person
            .HasPersonSeriesCount(true, FilterComparison.NotEqual, 2)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.Name == "Two Series Person");
    }

    [Fact]
    public async Task HasPersonSeriesCount_GreaterThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonSeriesCount(context);

        var result = await context.Person
            .HasPersonSeriesCount(true, FilterComparison.GreaterThan, 1)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Two Series Person", result[0].Name);
    }

    [Fact]
    public async Task HasPersonSeriesCount_GreaterThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonSeriesCount(context);

        var result = await context.Person
            .HasPersonSeriesCount(true, FilterComparison.GreaterThanEqual, 1)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "One Series Person");
        Assert.Contains(result, p => p.Name == "Two Series Person");
    }

    [Fact]
    public async Task HasPersonSeriesCount_LessThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonSeriesCount(context);

        var result = await context.Person
            .HasPersonSeriesCount(true, FilterComparison.LessThan, 2)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "No Series Person");
        Assert.Contains(result, p => p.Name == "One Series Person");
    }

    [Fact]
    public async Task HasPersonSeriesCount_LessThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonSeriesCount(context);

        var result = await context.Person
            .HasPersonSeriesCount(true, FilterComparison.LessThanEqual, 1)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "No Series Person");
        Assert.Contains(result, p => p.Name == "One Series Person");
    }

    [Fact]
    public async Task HasPersonSeriesCount_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonSeriesCount(context);

        var result = await context.Person
            .HasPersonSeriesCount(false, FilterComparison.Equal, 2)
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    #endregion

    #region HasPersonChapterCount

    private async Task SetupHasPersonChapterCount(DataContext context)
    {
        var personNone = new PersonBuilder("No Chapters Person").Build();
        var personOne = new PersonBuilder("One Chapter Person").Build();
        var personThree = new PersonBuilder("Three Chapters Person").Build();

        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(30)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10)
                        .WithPerson(personOne, PersonRole.Writer)
                        .WithPerson(personThree, PersonRole.Writer)
                        .Build())
                    .WithChapter(new ChapterBuilder("2").WithPages(10)
                        .WithPerson(personThree, PersonRole.Writer)
                        .Build())
                    .WithChapter(new ChapterBuilder("3").WithPages(10)
                        .WithPerson(personThree, PersonRole.Writer)
                        .Build())
                    .Build())
                .Build())
            .Build();

        context.Person.Add(personNone);
        context.Library.Add(library);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasPersonChapterCount_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        var result = await context.Person
            .HasPersonChapterCount(true, FilterComparison.Equal, 3)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Three Chapters Person", result[0].Name);
    }

    [Fact]
    public async Task HasPersonChapterCount_Equal_Zero_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        var result = await context.Person
            .HasPersonChapterCount(true, FilterComparison.Equal, 0)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("No Chapters Person", result[0].Name);
    }

    [Fact]
    public async Task HasPersonChapterCount_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        var result = await context.Person
            .HasPersonChapterCount(true, FilterComparison.NotEqual, 3)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.Name == "Three Chapters Person");
    }

    [Fact]
    public async Task HasPersonChapterCount_GreaterThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        var result = await context.Person
            .HasPersonChapterCount(true, FilterComparison.GreaterThan, 1)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Three Chapters Person", result[0].Name);
    }

    [Fact]
    public async Task HasPersonChapterCount_GreaterThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        var result = await context.Person
            .HasPersonChapterCount(true, FilterComparison.GreaterThanEqual, 1)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "One Chapter Person");
        Assert.Contains(result, p => p.Name == "Three Chapters Person");
    }

    [Fact]
    public async Task HasPersonChapterCount_LessThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        var result = await context.Person
            .HasPersonChapterCount(true, FilterComparison.LessThan, 3)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "No Chapters Person");
        Assert.Contains(result, p => p.Name == "One Chapter Person");
    }

    [Fact]
    public async Task HasPersonChapterCount_LessThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        var result = await context.Person
            .HasPersonChapterCount(true, FilterComparison.LessThanEqual, 1)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "No Chapters Person");
        Assert.Contains(result, p => p.Name == "One Chapter Person");
    }

    [Fact]
    public async Task HasPersonChapterCount_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        var result = await context.Person
            .HasPersonChapterCount(false, FilterComparison.Equal, 3)
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task HasPersonChapterCount_UnsupportedComparison_ThrowsException()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPersonChapterCount(context);

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await context.Person.HasPersonChapterCount(true, FilterComparison.Contains, 1).ToListAsync();
        });
    }

    #endregion
}
