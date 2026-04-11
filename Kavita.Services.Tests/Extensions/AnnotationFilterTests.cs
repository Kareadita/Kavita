using Kavita.Database;
using Kavita.Database.Extensions.Filters;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Extensions;

public class AnnotationFilterTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{

    #region IsLikedBy

    /// <summary>
    /// Creates 3 annotations:
    ///   - "A1" liked by users 1 and 2
    ///   - "A2" liked by user 2 only
    ///   - "A3" no likes
    /// </summary>
    private static async Task SetupIsLikedBy(DataContext context)
    {
        var user = new AppUserBuilder("testuser", "test@localhost").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        // Get the seeded library
        var library = await context.Library.Include(l => l.Series).FirstAsync();

        var series = new SeriesBuilder("TestSeries").WithPages(10)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                .Build())
            .Build();

        library.Series.Add(series);
        await context.SaveChangesAsync();

        var chapter = await context.Chapter.FirstAsync();
        var volume = await context.Volume.FirstAsync();

        context.AppUserAnnotation.AddRange(
            new AppUserAnnotation
            {
                XPath = "/p[1]",
                SelectedText = "A1",
                LibraryId = library.Id,
                SeriesId = series.Id,
                VolumeId = volume.Id,
                ChapterId = chapter.Id,
                AppUserId = user.Id,
                Likes = new List<int> { 1, 2 }
            },
            new AppUserAnnotation
            {
                XPath = "/p[2]",
                SelectedText = "A2",
                LibraryId = library.Id,
                SeriesId = series.Id,
                VolumeId = volume.Id,
                ChapterId = chapter.Id,
                AppUserId = user.Id,
                Likes = new List<int> { 2 }
            },
            new AppUserAnnotation
            {
                XPath = "/p[3]",
                SelectedText = "A3",
                LibraryId = library.Id,
                SeriesId = series.Id,
                VolumeId = volume.Id,
                ChapterId = chapter.Id,
                AppUserId = user.Id,
                Likes = new List<int>()
            }
        );

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task IsLikedBy_Equal_Works()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        var result = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.Equal, new List<int> { 1 })
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("A1", result[0].SelectedText);
    }

    [Fact]
    public async Task IsLikedBy_NotEqual_ExcludesMatchingUser()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        // NotEqual user 1 should return A2 (liked by 2 only) and A3 (no likes)
        var result = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.NotEqual, new List<int> { 1 })
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, a => a.SelectedText == "A1");
    }

    [Fact]
    public async Task IsLikedBy_NotEqual_DiffersFromEqual()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        // This is the regression test: Equal and NotEqual must return disjoint sets
        var equalResult = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.Equal, new List<int> { 1 })
            .ToListAsync();

        var notEqualResult = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.NotEqual, new List<int> { 1 })
            .ToListAsync();

        var equalIds = equalResult.Select(a => a.Id).ToHashSet();
        var notEqualIds = notEqualResult.Select(a => a.Id).ToHashSet();

        // They must not overlap
        Assert.Empty(equalIds.Intersect(notEqualIds));
        // Together they should cover all 3 annotations
        Assert.Equal(3, equalIds.Union(notEqualIds).Count());
    }

    [Fact]
    public async Task IsLikedBy_Contains_ReturnsAnyMatch()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        // Contains [1, 2] should return A1 (has both) and A2 (has 2)
        var result = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.Contains, new List<int> { 1, 2 })
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.SelectedText == "A1");
        Assert.Contains(result, a => a.SelectedText == "A2");
    }

    [Fact]
    public async Task IsLikedBy_NotContains_ExcludesAnyMatch()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        // NotContains [1, 2] should return only A3 (no likes)
        var result = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.NotContains, new List<int> { 1, 2 })
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("A3", result[0].SelectedText);
    }

    [Fact]
    public async Task IsLikedBy_MustContains_RequiresAllUsers()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        // MustContains [1, 2] should return only A1 (which has both 1 and 2)
        var result = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.MustContains, new List<int> { 1, 2 })
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("A1", result[0].SelectedText);
    }

    [Fact]
    public async Task IsLikedBy_MustContains_SingleUser_Works()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        // MustContains [2] should return A1 and A2 (both contain user 2)
        var result = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.MustContains, new List<int> { 2 })
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.SelectedText == "A1");
        Assert.Contains(result, a => a.SelectedText == "A2");
    }

    [Fact]
    public async Task IsLikedBy_MustContains_NoAnnotationHasAll_ReturnsEmpty()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        // MustContains [1, 2, 99] - no annotation has all three
        var result = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.MustContains, new List<int> { 1, 2, 99 })
            .ToListAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task IsLikedBy_ConditionFalse_ReturnsAll()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        var result = await context.AppUserAnnotation
            .IsLikedBy(false, FilterComparison.Equal, new List<int> { 1 })
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task IsLikedBy_EmptyList_ReturnsAll()
    {
        var (_, context, _) = await CreateDatabase();
        await SetupIsLikedBy(context);

        var result = await context.AppUserAnnotation
            .IsLikedBy(true, FilterComparison.Contains, new List<int>())
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    #endregion
}
