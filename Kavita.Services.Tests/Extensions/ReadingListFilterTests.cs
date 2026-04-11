using Kavita.Database;
using Kavita.Database.Extensions.Filters;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Services.Builders;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Extensions;

public class ReadingListFilterTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{

    #region HasTitle

    private async Task SetupHasTitle(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(library);
        await context.SaveChangesAsync();

        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.ReadingList.AddRange(
            new ReadingListBuilder("Alpha List").WithAppUserId(user.Id).Build(),
            new ReadingListBuilder("Beta Collection").WithAppUserId(user.Id).Build()
        );
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasTitle_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTitle(context);

        var result = await context.ReadingList
            .HasTitle(true, FilterComparison.Equal, "Alpha List")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Alpha List", result[0].Title);
    }

    [Fact]
    public async Task HasTitle_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTitle(context);

        var result = await context.ReadingList
            .HasTitle(true, FilterComparison.NotEqual, "Alpha List")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Beta Collection", result[0].Title);
    }

    [Fact]
    public async Task HasTitle_BeginsWith_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTitle(context);

        var result = await context.ReadingList
            .HasTitle(true, FilterComparison.BeginsWith, "Alpha")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Alpha List", result[0].Title);
    }

    [Fact]
    public async Task HasTitle_EndsWith_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTitle(context);

        var result = await context.ReadingList
            .HasTitle(true, FilterComparison.EndsWith, "Collection")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Beta Collection", result[0].Title);
    }

    [Fact]
    public async Task HasTitle_Matches_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTitle(context);

        var result = await context.ReadingList
            .HasTitle(true, FilterComparison.Matches, "pha Li")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Alpha List", result[0].Title);
    }

    [Fact]
    public async Task HasTitle_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTitle(context);

        var result = await context.ReadingList
            .HasTitle(false, FilterComparison.Equal, "Alpha List")
            .ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task HasTitle_EmptyString_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTitle(context);

        var result = await context.ReadingList
            .HasTitle(true, FilterComparison.Equal, string.Empty)
            .ToListAsync();

        Assert.Equal(2, result.Count);
    }

    #endregion

    #region HasReleaseYear

    private async Task SetupHasReleaseYear(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(library);
        await context.SaveChangesAsync();

        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.ReadingList.AddRange(
            new ReadingListBuilder("RL 2000").WithAppUserId(user.Id).WithStartingYear(2000).Build(),
            new ReadingListBuilder("RL 2020").WithAppUserId(user.Id).WithStartingYear(2020).Build(),
            new ReadingListBuilder("RL 2025").WithAppUserId(user.Id).WithStartingYear(2025).Build()
        );
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasReleaseYear_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.Equal, 2020)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL 2020", result[0].Title);
    }

    [Fact]
    public async Task HasReleaseYear_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.NotEqual, 2020)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.Title == "RL 2020");
    }

    [Fact]
    public async Task HasReleaseYear_GreaterThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.GreaterThan, 2000)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL 2020");
        Assert.Contains(result, r => r.Title == "RL 2025");
    }

    [Fact]
    public async Task HasReleaseYear_GreaterThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.GreaterThanEqual, 2020)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL 2020");
        Assert.Contains(result, r => r.Title == "RL 2025");
    }

    [Fact]
    public async Task HasReleaseYear_LessThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.LessThan, 2025)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL 2000");
        Assert.Contains(result, r => r.Title == "RL 2020");
    }

    [Fact]
    public async Task HasReleaseYear_LessThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.LessThanEqual, 2020)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL 2000");
        Assert.Contains(result, r => r.Title == "RL 2020");
    }

    [Fact]
    public async Task HasReleaseYear_IsInLast_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var years = DateTime.Now.Year - 2020;

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.IsInLast, years)
            .ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task HasReleaseYear_IsNotInLast_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var years = DateTime.Now.Year - 2020;

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.IsNotInLast, years)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL 2000", result[0].Title);
    }

    [Fact]
    public async Task HasReleaseYear_IsEmpty_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();

        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(library);
        await context.SaveChangesAsync();

        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.ReadingList.AddRange(
            new ReadingListBuilder("Has Year").WithAppUserId(user.Id).WithStartingYear(2020).Build(),
            new ReadingListBuilder("No Year").WithAppUserId(user.Id).Build()
        );
        await context.SaveChangesAsync();

        var result = await context.ReadingList
            .HasReleaseYear(true, FilterComparison.IsEmpty, 0)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("No Year", result[0].Title);
    }

    [Fact]
    public async Task HasReleaseYear_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var result = await context.ReadingList
            .HasReleaseYear(false, FilterComparison.Equal, 2020)
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    #endregion

    #region HasItemCount

    private async Task SetupHasItemCount(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .WithChapter(new ChapterBuilder("2").WithPages(10).Build())
                    .WithChapter(new ChapterBuilder("3").WithPages(10).Build())
                    .WithChapter(new ChapterBuilder("4").WithPages(10).Build())
                    .WithChapter(new ChapterBuilder("5").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(library);
        await context.SaveChangesAsync();

        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var series = await context.Series.Include(s => s.Volumes).ThenInclude(v => v.Chapters).FirstAsync();
        var chapters = series.Volumes.First().Chapters.OrderBy(c => c.MinNumber).ToList();

        // RL with 0 items
        var rl0 = new ReadingListBuilder("RL Empty").WithAppUserId(user.Id).Build();

        // RL with 2 items
        var rl2 = new ReadingListBuilder("RL Two").WithAppUserId(user.Id)
            .WithItem(new ReadingListItemBuilder(0, series.Id, series.Volumes.First().Id, chapters[0].Id).Build())
            .WithItem(new ReadingListItemBuilder(1, series.Id, series.Volumes.First().Id, chapters[1].Id).Build())
            .Build();

        // RL with 5 items
        var rl5 = new ReadingListBuilder("RL Five").WithAppUserId(user.Id)
            .WithItem(new ReadingListItemBuilder(0, series.Id, series.Volumes.First().Id, chapters[0].Id).Build())
            .WithItem(new ReadingListItemBuilder(1, series.Id, series.Volumes.First().Id, chapters[1].Id).Build())
            .WithItem(new ReadingListItemBuilder(2, series.Id, series.Volumes.First().Id, chapters[2].Id).Build())
            .WithItem(new ReadingListItemBuilder(3, series.Id, series.Volumes.First().Id, chapters[3].Id).Build())
            .WithItem(new ReadingListItemBuilder(4, series.Id, series.Volumes.First().Id, chapters[4].Id).Build())
            .Build();

        context.ReadingList.AddRange(rl0, rl2, rl5);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasItemCount_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasItemCount(context);

        var result = await context.ReadingList
            .HasItemCount(true, FilterComparison.Equal, 2)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL Two", result[0].Title);
    }

    [Fact]
    public async Task HasItemCount_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasItemCount(context);

        var result = await context.ReadingList
            .HasItemCount(true, FilterComparison.NotEqual, 2)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.Title == "RL Two");
    }

    [Fact]
    public async Task HasItemCount_GreaterThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasItemCount(context);

        var result = await context.ReadingList
            .HasItemCount(true, FilterComparison.GreaterThan, 2)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL Five", result[0].Title);
    }

    [Fact]
    public async Task HasItemCount_GreaterThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasItemCount(context);

        var result = await context.ReadingList
            .HasItemCount(true, FilterComparison.GreaterThanEqual, 2)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL Two");
        Assert.Contains(result, r => r.Title == "RL Five");
    }

    [Fact]
    public async Task HasItemCount_LessThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasItemCount(context);

        var result = await context.ReadingList
            .HasItemCount(true, FilterComparison.LessThan, 5)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL Empty");
        Assert.Contains(result, r => r.Title == "RL Two");
    }

    [Fact]
    public async Task HasItemCount_LessThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasItemCount(context);

        var result = await context.ReadingList
            .HasItemCount(true, FilterComparison.LessThanEqual, 2)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL Empty");
        Assert.Contains(result, r => r.Title == "RL Two");
    }

    [Fact]
    public async Task HasItemCount_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasItemCount(context);

        var result = await context.ReadingList
            .HasItemCount(false, FilterComparison.Equal, 2)
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    #endregion

    #region HasTags

    private async Task SetupHasTags(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(library);
        await context.SaveChangesAsync();

        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tag1 = new ReadingListTag { Title = "Favorites", NormalizedTitle = "favorites" };
        var tag2 = new ReadingListTag { Title = "ToRead", NormalizedTitle = "toread" };

        context.ReadingList.AddRange(
            new ReadingListBuilder("RL Both Tags").WithAppUserId(user.Id)
                .WithTag(tag1).WithTag(tag2).Build(),
            new ReadingListBuilder("RL One Tag").WithAppUserId(user.Id)
                .WithTag(tag1).Build(),
            new ReadingListBuilder("RL No Tags").WithAppUserId(user.Id).Build()
        );
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasTags_Contains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTags(context);

        var tagIds = await context.Set<ReadingListTag>()
            .Where(t => t.NormalizedTitle == "favorites")
            .Select(t => t.Id)
            .ToListAsync();

        var result = await context.ReadingList
            .HasTags(true, FilterComparison.Contains, tagIds)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL Both Tags");
        Assert.Contains(result, r => r.Title == "RL One Tag");
    }

    [Fact]
    public async Task HasTags_NotContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTags(context);

        var tagIds = await context.Set<ReadingListTag>()
            .Where(t => t.NormalizedTitle == "favorites")
            .Select(t => t.Id)
            .ToListAsync();

        var result = await context.ReadingList
            .HasTags(true, FilterComparison.NotContains, tagIds)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL No Tags", result[0].Title);
    }

    [Fact]
    public async Task HasTags_MustContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTags(context);

        var tagIds = await context.Set<ReadingListTag>()
            .Select(t => t.Id)
            .ToListAsync();

        var result = await context.ReadingList
            .HasTags(true, FilterComparison.MustContains, tagIds)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL Both Tags", result[0].Title);
    }

    [Fact]
    public async Task HasTags_IsEmpty_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTags(context);

        var result = await context.ReadingList
            .HasTags(true, FilterComparison.IsEmpty, new List<int>())
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL No Tags", result[0].Title);
    }

    [Fact]
    public async Task HasTags_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasTags(context);

        var result = await context.ReadingList
            .HasTags(false, FilterComparison.Contains, new List<int> { 1 })
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    #endregion

    #region HasPeople

    private async Task SetupHasPeople(DataContext context)
    {
        var writer = new PersonBuilder("Writer Person").Build();
        var artist = new PersonBuilder("Artist Person").Build();

        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("SeriesA").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10)
                        .WithPerson(writer, PersonRole.Writer)
                        .WithPerson(artist, PersonRole.CoverArtist)
                        .Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("SeriesB").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10)
                        .WithPerson(writer, PersonRole.Writer)
                        .Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("SeriesC").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(library);
        await context.SaveChangesAsync();

        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var seriesA = await context.Series.Include(s => s.Volumes).ThenInclude(v => v.Chapters)
            .FirstAsync(s => s.Name == "SeriesA");
        var seriesB = await context.Series.Include(s => s.Volumes).ThenInclude(v => v.Chapters)
            .FirstAsync(s => s.Name == "SeriesB");
        var seriesC = await context.Series.Include(s => s.Volumes).ThenInclude(v => v.Chapters)
            .FirstAsync(s => s.Name == "SeriesC");

        var chapterA = seriesA.Volumes.First().Chapters.First();
        var chapterB = seriesB.Volumes.First().Chapters.First();
        var chapterC = seriesC.Volumes.First().Chapters.First();

        // RL with writer+artist chapter
        var rl1 = new ReadingListBuilder("RL Writer+Artist").WithAppUserId(user.Id)
            .WithItem(new ReadingListItemBuilder(0, seriesA.Id, seriesA.Volumes.First().Id, chapterA.Id).Build())
            .Build();

        // RL with writer-only chapter
        var rl2 = new ReadingListBuilder("RL Writer Only").WithAppUserId(user.Id)
            .WithItem(new ReadingListItemBuilder(0, seriesB.Id, seriesB.Volumes.First().Id, chapterB.Id).Build())
            .Build();

        // RL with no people
        var rl3 = new ReadingListBuilder("RL No People").WithAppUserId(user.Id)
            .WithItem(new ReadingListItemBuilder(0, seriesC.Id, seriesC.Volumes.First().Id, chapterC.Id).Build())
            .Build();

        context.ReadingList.AddRange(rl1, rl2, rl3);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task HasPeople_Contains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPeople(context);

        var writerIds = await context.Person
            .Where(p => p.Name == "Writer Person")
            .Select(p => p.Id)
            .ToListAsync();

        var result = await context.ReadingList
            .HasPeople(true, FilterComparison.Contains, writerIds, PersonRole.Writer)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Title == "RL Writer+Artist");
        Assert.Contains(result, r => r.Title == "RL Writer Only");
    }

    [Fact]
    public async Task HasPeople_NotContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPeople(context);

        var writerIds = await context.Person
            .Where(p => p.Name == "Writer Person")
            .Select(p => p.Id)
            .ToListAsync();

        var result = await context.ReadingList
            .HasPeople(true, FilterComparison.NotContains, writerIds, PersonRole.Writer)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL No People", result[0].Title);
    }

    [Fact]
    public async Task HasPeople_MustContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPeople(context);

        var personIds = await context.Person
            .Select(p => p.Id)
            .ToListAsync();

        // MustContains with Writer role — both writer person and artist person must be writers
        // Only writer person is a writer, so only lists containing writer person match
        var result = await context.ReadingList
            .HasPeople(true, FilterComparison.MustContains, new List<int> { personIds[0] }, PersonRole.Writer)
            .ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task HasPeople_IsEmpty_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPeople(context);

        var result = await context.ReadingList
            .HasPeople(true, FilterComparison.IsEmpty, new List<int>(), PersonRole.Writer)
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("RL No People", result[0].Title);
    }

    [Fact]
    public async Task HasPeople_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPeople(context);

        var result = await context.ReadingList
            .HasPeople(false, FilterComparison.Contains, new List<int> { 1 }, PersonRole.Writer)
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    #endregion
}
