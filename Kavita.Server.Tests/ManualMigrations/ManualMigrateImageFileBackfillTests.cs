using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.User;
using Kavita.Models.Images;
using Kavita.Server.ManualMigrations.v0._9._2;
using Kavita.Services.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Server.Tests.ManualMigrations;

// Seeds the obsolete CoverImage, color and bookmark FileName columns the backfill reads
#pragma warning disable CS0618


/// <param name="testOutputHelper"></param>
public class ManualMigrateImageFileBackfillTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private static readonly string FaviconDirectory = ConfigDirectory + "favicons/";
    private static readonly string PublisherDirectory = ConfigDirectory + "images/publishers/";

    private readonly MockFileSystem _fileSystem = CreateFileSystem();

    [Fact]
    public async Task SharedName_OneChapterRow()
    {
        var (_, context, _) = await CreateDatabase();
        var series = await AddSeries(context, "v1_c1.webp", "v1_c1.webp", "v1_c1.webp");

        await RunBackfill(context);

        var row = await context.ImageFile.SingleAsync();
        Assert.Equal(ImageKind.ChapterCover, row.Kind);
        Assert.Equal("v1_c1.webp", row.FileName);
        Assert.Equal(row.Id, (await context.Chapter.SingleAsync()).CoverImageFileId);
        Assert.Equal(row.Id, (await context.Volume.SingleAsync()).CoverImageFileId);
        Assert.Equal(row.Id, (await context.Series.SingleAsync(s => s.Id == series.Id)).CoverImageFileId);
    }

    [Fact]
    public async Task OwnNames_OwnRows()
    {
        var (_, context, _) = await CreateDatabase();
        await AddSeries(context, "v1_c1.webp", "v5.webp", "series3.webp");

        await RunBackfill(context);

        var kinds = await context.ImageFile.ToDictionaryAsync(f => f.FileName, f => f.Kind);
        Assert.Equal(3, kinds.Count);
        Assert.Equal(ImageKind.ChapterCover, kinds["v1_c1.webp"]);
        Assert.Equal(ImageKind.VolumeCover, kinds["v5.webp"]);
        Assert.Equal(ImageKind.SeriesCover, kinds["series3.webp"]);
        Assert.Equal((await context.ImageFile.SingleAsync(f => f.FileName == "v5.webp")).Id,
            (await context.Volume.SingleAsync()).CoverImageFileId);
    }

    [Fact]
    public async Task ChapterOrigin()
    {
        var (_, context, _) = await CreateDatabase();
        var chapters = new[]
        {
            NewChapter("c1.webp", true, MetadataSettingField.ChapterCovers),
            NewChapter("c2.webp", false, MetadataSettingField.ChapterCovers),
            NewChapter("c3.webp", true),
            NewChapter("c4.webp", false),
        };
        await AddSeries(context, chapters);

        await RunBackfill(context);

        var origins = await context.ImageFile.ToDictionaryAsync(f => f.FileName, f => f.Origin);
        Assert.Equal(ImageOrigin.KavitaPlus, origins["c1.webp"]);
        Assert.Equal(ImageOrigin.Scan, origins["c2.webp"]);
        Assert.Equal(ImageOrigin.Upload, origins["c3.webp"]);
        Assert.Equal(ImageOrigin.Scan, origins["c4.webp"]);
    }

    [Fact]
    public async Task SeriesOrigin_ReadsMetadataOverrides()
    {
        var (_, context, _) = await CreateDatabase();
        var series = await AddSeries(context, "v1_c1.webp", "v1_c1.webp", "series1.webp");
        var tracked = await context.Series.Include(s => s.Metadata).SingleAsync(s => s.Id == series.Id);
        tracked.CoverImageLocked = true;
        tracked.Metadata.KPlusOverrides = [MetadataSettingField.Covers];
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await RunBackfill(context);

        Assert.Equal(ImageOrigin.KavitaPlus, (await context.ImageFile.SingleAsync(f => f.FileName == "series1.webp")).Origin);
    }

    [Fact]
    public async Task PersonOrigin()
    {
        var (_, context, _) = await CreateDatabase();
        context.Person.AddRange(
            NewPerson("Locked AniList", "p1.webp", true, 5),
            NewPerson("Unlocked AniList", "p2.webp", false, 5),
            NewPerson("Locked No Ids", "p3.webp", true, 0));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await RunBackfill(context);

        var origins = await context.ImageFile.ToDictionaryAsync(f => f.FileName, f => f.Origin);
        Assert.Equal(ImageOrigin.KavitaPlus, origins["p1.webp"]);
        Assert.Equal(ImageOrigin.Upload, origins["p2.webp"]);
        Assert.Equal(ImageOrigin.Upload, origins["p3.webp"]);
        Assert.All(await context.ImageFile.ToListAsync(), f => Assert.Equal(ImageKind.PersonCover, f.Kind));
    }

    [Fact]
    public async Task SecondRun_AddsNothing()
    {
        var (_, context, _) = await CreateDatabase();
        await AddSeries(context, "v1_c1.webp", "v5.webp", "series3.webp");
        _fileSystem.AddFile(FaviconDirectory + "anilist.co.png", new MockFileData([1]));

        await RunBackfill(context);
        var firstCount = await context.ImageFile.CountAsync();
        await context.ManualMigrationHistory.Where(m => m.Name == nameof(ManualMigrateImageFileBackfill)).ExecuteDeleteAsync();
        await RunBackfill(context);

        Assert.Equal(4, firstCount);
        Assert.Equal(firstCount, await context.ImageFile.CountAsync());
    }

    [Fact]
    public async Task MoreThanOneBatch_AllLinked()
    {
        var (_, context, _) = await CreateDatabase();
        var chapters = Enumerable.Range(0, 2500).Select(i => NewChapter($"c{i}.webp", false)).ToArray();
        await AddSeries(context, chapters);

        await RunBackfill(context);

        Assert.Equal(2500, await context.ImageFile.CountAsync(f => f.Kind == ImageKind.ChapterCover));
        Assert.Equal(0, await context.Chapter.CountAsync(c => c.CoverImageFileId == null));
    }

    [Fact]
    public async Task LinkingLeavesLastModified()
    {
        var (_, context, _) = await CreateDatabase();
        var series = await AddSeries(context, "v1_c1.webp", "v1_c1.webp", "v1_c1.webp");
        var before = await context.Chapter.Select(c => c.LastModifiedUtc).SingleAsync();
        var seriesBefore = await context.Series.Where(s => s.Id == series.Id).Select(s => s.LastModifiedUtc).SingleAsync();

        await RunBackfill(context);

        Assert.Equal(before, await context.Chapter.Select(c => c.LastModifiedUtc).SingleAsync());
        Assert.Equal(seriesBefore, await context.Series.Where(s => s.Id == series.Id).Select(s => s.LastModifiedUtc).SingleAsync());
    }

    [Fact]
    public async Task EmptyName_NoRow()
    {
        var (_, context, _) = await CreateDatabase();
        await AddSeries(context, "", "", "");

        await RunBackfill(context);

        Assert.Equal(0, await context.ImageFile.CountAsync());
        Assert.Null((await context.Chapter.SingleAsync()).CoverImageFileId);
    }

    [Fact]
    public async Task CopiesColors()
    {
        var (_, context, _) = await CreateDatabase();
        var chapter = NewChapter("c1.webp", false);
        chapter.PrimaryColor = "#112233";
        chapter.SecondaryColor = "#445566";
        await AddSeries(context, chapter);

        await RunBackfill(context);

        var row = await context.ImageFile.SingleAsync();
        Assert.Equal("#112233", row.PrimaryColor);
        Assert.Equal("#445566", row.SecondaryColor);
    }

    [Fact]
    public async Task Bookmark_RowPerFile()
    {
        var (_, context, _) = await CreateDatabase();
        var series = await AddSeries(context, "", "", "");
        var chapter = await context.Chapter.SingleAsync();
        var user = new AppUserBuilder("reader", "reader@kavita.invalid").Build();
        foreach (var page in new[] { 1, 2 })
        {
            user.Bookmarks.Add(new AppUserBookmark
            {
                Page = page,
                SeriesId = series.Id,
                VolumeId = chapter.VolumeId,
                ChapterId = chapter.Id,
                FileName = $"1/{series.Id}/{chapter.Id}/00{page}.webp",
            });
        }
        context.AppUser.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await RunBackfill(context);

        var rows = await context.ImageFile.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(ImageKind.Bookmark, r.Kind));
        Assert.All(rows, r => Assert.Equal(ImageOrigin.Legacy, r.Origin));
        Assert.Equal(0, await context.AppUserBookmark.CountAsync(b => b.ImageFileId == null));
    }

    [Fact]
    public async Task FaviconsAndPublishers()
    {
        var (_, context, _) = await CreateDatabase();
        _fileSystem.AddFile(FaviconDirectory + "anilist.co.png", new MockFileData([1]));
        _fileSystem.AddFile(FaviconDirectory + "anilist.co.webp", new MockFileData([1]));
        _fileSystem.AddFile(PublisherDirectory + "Seven Seas.png", new MockFileData([1]));

        await RunBackfill(context);

        var rows = await context.ImageFile.ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(2, rows.Count(r => r.Kind == ImageKind.Favicon && r.Key == "anilist.co"));
        Assert.Single(rows, r => r.Kind == ImageKind.Publisher && r.Key == "Seven Seas" && r.FileName == "Seven Seas.png");
    }

    private async Task RunBackfill(DataContext context)
    {
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.FileSystem.Returns(_fileSystem);
        directoryService.FaviconDirectory.Returns(FaviconDirectory);
        directoryService.PublisherDirectory.Returns(PublisherDirectory);

        await new ManualMigrateImageFileBackfill(directoryService).RunAsync(context, Substitute.For<ILogger<Program>>());
        context.ChangeTracker.Clear();
    }

    private static Chapter NewChapter(string cover, bool locked, params MetadataSettingField[] overrides)
    {
        var chapter = new ChapterBuilder("1").WithCoverImage(cover).Build();
        chapter.CoverImageLocked = locked;
        chapter.KPlusOverrides = overrides.ToList();
        return chapter;
    }

    private static Person NewPerson(string name, string cover, bool locked, int aniListId)
    {
        var person = new PersonBuilder(name).Build();
        person.CoverImage = cover;
        person.CoverImageLocked = locked;
        person.AniListId = aniListId;
        return person;
    }

    private static Task<Series> AddSeries(DataContext context, string chapterCover, string volumeCover, string seriesCover)
    {
        var chapter = new ChapterBuilder("1").WithCoverImage(chapterCover).Build();
        return AddSeries(context, chapter, volumeCover, seriesCover);
    }

    private static Task<Series> AddSeries(DataContext context, params Chapter[] chapters)
    {
        return AddSeries(context, chapters, "", "");
    }

    private static Task<Series> AddSeries(DataContext context, Chapter chapter, string volumeCover, string seriesCover)
    {
        return AddSeries(context, [chapter], volumeCover, seriesCover);
    }

    private static async Task<Series> AddSeries(DataContext context, Chapter[] chapters, string volumeCover, string seriesCover)
    {
        var volume = new VolumeBuilder("1").WithChapters(chapters).WithCoverImage(volumeCover).Build();
        var series = new SeriesBuilder("Series").WithVolume(volume).WithCoverImage(seriesCover).Build();
        var library = new LibraryBuilder("Manga").WithSeries(series).Build();
        context.Library.Add(library);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return series;
    }
}
