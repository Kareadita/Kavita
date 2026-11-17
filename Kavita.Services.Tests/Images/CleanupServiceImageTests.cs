using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;
using Kavita.Services.Builders;
using Kavita.Services.Images;
using Kavita.Services.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;
using Image = NetVips.Image;

namespace Kavita.Services.Tests.Images;

[Collection(NetVipsGlobalStateCollection.Name)]
public class CleanupServiceImageTests : AbstractDbTest, IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), "kavita-imagecleanup-" + Guid.NewGuid().ToString("N"));
    private readonly string _covers;
    private readonly string _favicons;
    private readonly string _publishers;
    private readonly string _bookmarks;
    private readonly int _maxFiles = NetVips.Cache.MaxFiles;

    public CleanupServiceImageTests(ITestOutputHelper output) : base(output)
    {
        _covers = Path.Join(_root, "covers");
        _favicons = Path.Join(_root, "favicons");
        _publishers = Path.Join(_root, "images", "publishers");
        _bookmarks = Path.Join(_root, "bookmarks");
        // Matches Program.cs, otherwise libvips' operation cache keeps every file open and Windows refuses the delete
        NetVips.Cache.MaxFiles = 0;
        foreach (var directory in new[] { _covers, _favicons, _publishers, _bookmarks })
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void Dispose()
    {
        NetVips.Cache.MaxFiles = _maxFiles;
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Cleanup_FormatChanged_AllHoldersRenamed()
    {
        var (service, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var volume = series.Volumes[0];
        var chapter = volume.Chapters[0];
        var person = new PersonBuilder("Jun Mochizuki").Build();
        context.Person.Add(person);
        var library = await context.Library.FirstAsync();
        await context.SaveChangesAsync();

        var chapterRow = AddRow(context, _covers, ImageKind.ChapterCover, $"v{volume.Id}_c{chapter.Id}.png", EncodeFormat.PNG);
        var personRow = AddRow(context, _covers, ImageKind.PersonCover, $"person{person.Id}.png", EncodeFormat.PNG);
        var libraryRow = AddRow(context, _covers, ImageKind.LibraryIcon, $"l{library.Id}.png", EncodeFormat.PNG, 64, 64);
        Link(chapter, chapterRow);
        Link(volume, chapterRow);
        Link(series, chapterRow);
        Link(person, personRow);
        Link(library, libraryRow);
        await context.SaveChangesAsync();

        await service.CleanupImages();

        context.ChangeTracker.Clear();
        foreach (var (row, stem) in new[] { (chapterRow, $"v{volume.Id}_c{chapter.Id}"), (personRow, $"person{person.Id}"), (libraryRow, $"l{library.Id}") })
        {
            var saved = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
            Assert.Equal(stem + ".webp", saved.FileName);
            Assert.Equal(EncodeFormat.WEBP, saved.Format);
            Assert.True(File.Exists(Path.Join(_covers, stem + ".webp")));
            Assert.False(File.Exists(Path.Join(_covers, stem + ".png")));
        }

        var chapterName = $"v{volume.Id}_c{chapter.Id}.webp";
        Assert.Equal(chapterName, CoverImageOf(await context.Chapter.SingleAsync(c => c.Id == chapter.Id)));
        Assert.Equal(chapterName, CoverImageOf(await context.Volume.SingleAsync(v => v.Id == volume.Id)));
        Assert.Equal(chapterName, CoverImageOf(await context.Series.SingleAsync(s => s.Id == series.Id)));
        Assert.Equal($"person{person.Id}.webp", CoverImageOf(await context.Person.SingleAsync(p => p.Id == person.Id)));
        Assert.Equal($"l{library.Id}.webp", CoverImageOf(await context.Library.SingleAsync(l => l.Id == library.Id)));
    }

    [Fact]
    public async Task Cleanup_UnreferencedRow_DeletedWithFile()
    {
        var (service, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var held = AddRow(context, _covers, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.WEBP);
        Link(series, held);
        var orphan = AddRow(context, _covers, ImageKind.SeriesCover, "series999.webp", EncodeFormat.WEBP);
        var favicon = AddRow(context, _favicons, ImageKind.Favicon, "anilist.co.webp", EncodeFormat.WEBP, 32, 32);
        await context.SaveChangesAsync();

        await service.CleanupImages();

        context.ChangeTracker.Clear();
        Assert.False(await context.ImageFile.AnyAsync(f => f.Id == orphan.Id));
        Assert.False(File.Exists(Path.Join(_covers, "series999.webp")));
        Assert.True(await context.ImageFile.AnyAsync(f => f.Id == held.Id));
        Assert.True(File.Exists(Path.Join(_covers, held.FileName)));
        Assert.True(await context.ImageFile.AnyAsync(f => f.Id == favicon.Id));
        Assert.True(File.Exists(Path.Join(_favicons, "anilist.co.webp")));
    }

    [Fact]
    public async Task Cleanup_Bookmarks_Untouched()
    {
        var (service, context) = await Setup(EncodeFormat.WEBP);
        var bookmark = AddRow(context, _bookmarks, ImageKind.Bookmark, "1/1/1/1.png", EncodeFormat.PNG, 1200, 1800);
        await context.SaveChangesAsync();
        var path = Path.Join(_bookmarks, "1/1/1/1.png");
        var bytes = await File.ReadAllBytesAsync(path);

        await service.CleanupImages();

        context.ChangeTracker.Clear();
        var saved = await context.ImageFile.SingleAsync(f => f.Id == bookmark.Id);
        Assert.Equal("1/1/1/1.png", saved.FileName);
        Assert.Equal(EncodeFormat.PNG, saved.Format);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    private async Task<(ICleanupService Service, DataContext Context)> Setup(EncodeFormat format)
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        (await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.EncodeMediaAs)).Value = format.ToString();
        (await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.CoverImageSize)).Value = nameof(CoverImageSize.Default);
        (await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.BookmarkDirectory)).Value = _bookmarks;
        await context.SaveChangesAsync();

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.CoverImageDirectory.Returns(_covers);
        directoryService.FaviconDirectory.Returns(_favicons);
        directoryService.PublisherDirectory.Returns(_publishers);
        var pipeline = new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>());
        var imageFileService = new ImageFileService(unitOfWork, pipeline, directoryService, Substitute.For<IEventHub>(),
            Substitute.For<ILogger<ImageFileService>>());
        var service = new CleanupService(Substitute.For<ILogger<CleanupService>>(), unitOfWork, Substitute.For<IEventHub>(),
            directoryService, pipeline, imageFileService);

        return (service, context);
    }

    private static async Task<Series> SeedSeries(DataContext context)
    {
        var series = new SeriesBuilder("Pandora Hearts")
            .WithVolume(new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").Build()).Build())
            .Build();
        series.LibraryId = (await context.Library.FirstAsync()).Id;
        context.Series.Add(series);
        await context.SaveChangesAsync();
        return series;
    }

    private static ImageFile AddRow(DataContext context, string directory, ImageKind kind, string fileName, EncodeFormat format,
        int width = 320, int height = 455)
    {
        var path = Path.Join(directory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var black = Image.Black(width, height, bands: 3))
        using (var colored = black + new double[] { 200, 80, 40 })
        using (var image = colored.Cast(NetVips.Enums.BandFormat.Uchar))
        {
            if (format == EncodeFormat.PNG)
            {
                image.Pngsave(path);
            }
            else
            {
                image.Webpsave(path);
            }
        }

        var row = new ImageFile { Kind = kind, FileName = fileName, Origin = ImageOrigin.Scan, Format = format, Width = width, Height = height };
        context.ImageFile.Add(row);
        return row;
    }

#pragma warning disable CS0618
    private static void Link(IHasCoverImageFile entity, ImageFile row)
    {
        entity.CoverImageFile = row;
        ((IHasCoverImage) entity).CoverImage = row.FileName;
    }

    private static string? CoverImageOf(IHasCoverImage entity) => entity.CoverImage;
#pragma warning restore CS0618
}
