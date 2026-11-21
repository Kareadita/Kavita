using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.SignalR.Bodies;
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
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
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

        await service.CleanupImages(false);

        context.ChangeTracker.Clear();
        foreach (var (row, stem) in new[] { (chapterRow, $"v{volume.Id}_c{chapter.Id}"), (personRow, $"person{person.Id}"), (libraryRow, $"l{library.Id}") })
        {
            var saved = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
            Assert.Equal(stem + ".webp", saved.FileName);
            Assert.Equal(EncodeFormat.WEBP, saved.Format);
            Assert.True(File.Exists(Path.Join(_covers, stem + ".webp")));
            Assert.False(File.Exists(Path.Join(_covers, stem + ".png")));
        }

        Assert.Equal(chapterRow.Id, (await context.Chapter.SingleAsync(c => c.Id == chapter.Id)).CoverImageFileId);
        Assert.Equal(chapterRow.Id, (await context.Volume.SingleAsync(v => v.Id == volume.Id)).CoverImageFileId);
        Assert.Equal(chapterRow.Id, (await context.Series.SingleAsync(s => s.Id == series.Id)).CoverImageFileId);
        Assert.Equal(personRow.Id, (await context.Person.SingleAsync(p => p.Id == person.Id)).CoverImageFileId);
        Assert.Equal(libraryRow.Id, (await context.Library.SingleAsync(l => l.Id == library.Id)).CoverImageFileId);
    }

    [Fact]
    public async Task Cleanup_UnreferencedRow_DeletedWithFile()
    {
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var held = AddRow(context, _covers, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.WEBP);
        Link(series, held);
        var orphan = AddRow(context, _covers, ImageKind.SeriesCover, "series999.webp", EncodeFormat.WEBP);
        var favicon = AddRow(context, _favicons, ImageKind.Favicon, "anilist.co.webp", EncodeFormat.WEBP, 32, 32);
        await context.SaveChangesAsync();

        await service.CleanupImages(false);

        context.ChangeTracker.Clear();
        Assert.False(await context.ImageFile.AnyAsync(f => f.Id == orphan.Id));
        Assert.False(File.Exists(Path.Join(_covers, "series999.webp")));
        Assert.True(await context.ImageFile.AnyAsync(f => f.Id == held.Id));
        Assert.True(File.Exists(Path.Join(_covers, held.FileName)));
        Assert.True(await context.ImageFile.AnyAsync(f => f.Id == favicon.Id));
        Assert.True(File.Exists(Path.Join(_favicons, "anilist.co.webp")));
    }

    [Fact]
    public async Task Cleanup_UnreferencedBookmark_ReencodedNotDeleted()
    {
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
        var bookmark = AddRow(context, _bookmarks, ImageKind.Bookmark, "1/1/1/1.png", EncodeFormat.PNG, 1200, 1800);
        await context.SaveChangesAsync();

        await service.CleanupImages(false);

        context.ChangeTracker.Clear();
        var saved = await context.ImageFile.SingleAsync(f => f.Id == bookmark.Id);
        Assert.Equal("1/1/1/1.webp", saved.FileName);
        Assert.Equal(EncodeFormat.WEBP, saved.Format);
        Assert.Equal(1200, saved.Width);
        Assert.True(File.Exists(Path.Join(_bookmarks, "1/1/1/1.webp")));
        Assert.False(File.Exists(Path.Join(_bookmarks, "1/1/1/1.png")));
    }

    [Fact]
    public async Task Cleanup_Manual_SendsSummary()
    {
        var (service, context, eventHub, _, _) = await Setup(EncodeFormat.WEBP);
        AddRow(context, _bookmarks, ImageKind.Bookmark, "1/1/1/1.png", EncodeFormat.PNG, 1200, 1800);
        await context.SaveChangesAsync();

        await service.CleanupImages(true);

        var summary = SummaryOf(eventHub, MessageEventCode.MediaConversionSummary);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.Corrected);
    }

    [Fact]
    public async Task Cleanup_Nightly_NoSummary()
    {
        var (service, context, eventHub, _, _) = await Setup(EncodeFormat.WEBP);
        AddRow(context, _bookmarks, ImageKind.Bookmark, "1/1/1/1.png", EncodeFormat.PNG, 1200, 1800);
        await context.SaveChangesAsync();

        await service.CleanupImages(false);

        Assert.Null(SummaryOf(eventHub, MessageEventCode.MediaConversionSummary));
    }

    [Fact]
    public async Task FirstPass_LegacyBookmark_Reencoded()
    {
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
        var path = Path.Join(_bookmarks, "1/1/1/1.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteImage(path, EncodeFormat.PNG, 1200, 1800);
        var row = new ImageFile { Kind = ImageKind.Bookmark, FileName = "1/1/1/1.png", Origin = ImageOrigin.Legacy };
        context.ImageFile.Add(row);
        await context.SaveChangesAsync();

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        context.ChangeTracker.Clear();
        var saved = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
        Assert.Equal("1/1/1/1.webp", saved.FileName);
        Assert.Equal(EncodeFormat.WEBP, saved.Format);
        Assert.Equal(1200, saved.Width);
        Assert.Equal(1800, saved.Height);
        Assert.True(File.Exists(Path.Join(_bookmarks, "1/1/1/1.webp")));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task FirstPass_BackupFails_ChangesNothing()
    {
        var (service, context, _, backup, _) = await Setup(EncodeFormat.WEBP);
        backup.BackupDatabase(Arg.Any<CancellationToken>()).Returns(false);
        var series = await SeedSeries(context);
        var row = AddUnprobedRow(context, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.PNG, 1280, 1820);
        Link(series, row);
        await context.SaveChangesAsync();
        WriteImage(Path.Join(_covers, "v9_c9.webp"), EncodeFormat.WEBP, 320, 455);
        var bytes = await File.ReadAllBytesAsync(Path.Join(_covers, row.FileName));

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        context.ChangeTracker.Clear();
        var saved = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
        Assert.Null(saved.Format);
        Assert.Null(saved.Width);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Join(_covers, row.FileName)));
        Assert.True(File.Exists(Path.Join(_covers, "v9_c9.webp")));
        Assert.False(await HasMigrationHistory(context));
    }

    [Fact]
    public async Task FirstPass_NothingToDo_NoBackup()
    {
        var (service, context, eventHub, backup, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        Link(series, AddRow(context, _covers, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.WEBP));
        await context.SaveChangesAsync();

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        await backup.DidNotReceive().BackupDatabase(Arg.Any<CancellationToken>());
        Assert.Null(SummaryOf(eventHub));
        Assert.True(await HasMigrationHistory(context));
    }

    [Fact]
    public async Task FirstPass_Finished_WritesHistory()
    {
        var (service, context, _, backup, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        Link(series, AddUnprobedRow(context, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.PNG, 320, 455));
        await context.SaveChangesAsync();

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        Assert.True(await HasMigrationHistory(context));
        backup.ClearReceivedCalls();
        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));
        await backup.DidNotReceive().BackupDatabase(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FirstPass_OversizedLockedKPlusCover_Shrunk()
    {
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var row = AddUnprobedRow(context, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.WEBP, 1280, 1820);
        row.Origin = ImageOrigin.KavitaPlus;
        Link(series, row);
        series.CoverImageLocked = true;
        await context.SaveChangesAsync();

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        context.ChangeTracker.Clear();
        var saved = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
        Assert.Equal(EncodeFormat.WEBP, saved.Format);
        Assert.Equal(320, saved.Width);
        Assert.Equal(455, saved.Height);
        Assert.Equal(ImageOrigin.KavitaPlus, saved.Origin);
        Assert.True((await context.Series.SingleAsync(s => s.Id == series.Id)).CoverImageLocked);
        using var written = Image.NewFromBuffer(await File.ReadAllBytesAsync(Path.Join(_covers, saved.FileName)));
        Assert.Equal(320, written.Width);
    }

    [Fact]
    public async Task FirstPass_MislabeledFile_Fixed()
    {
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var chapter = series.Volumes[0].Chapters[0];
        var name = $"v{series.Volumes[0].Id}_c{chapter.Id}.webp";
        var row = AddUnprobedRow(context, ImageKind.ChapterCover, name, EncodeFormat.PNG, 320, 455);
        Link(chapter, row);
        await context.SaveChangesAsync();

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        context.ChangeTracker.Clear();
        var saved = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
        Assert.Equal(name, saved.FileName);
        Assert.Equal(EncodeFormat.WEBP, saved.Format);
        Assert.Equal(320, saved.Width);
        Assert.Equal(455, saved.Height);
        using var written = Image.NewFromBuffer(await File.ReadAllBytesAsync(Path.Join(_covers, name)));
        Assert.Equal("webpload_buffer", written.Get("vips-loader"));
    }

    [Fact]
    public async Task FirstPass_WrongNameRightContent_RenamedOnly()
    {
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var row = AddUnprobedRow(context, ImageKind.SeriesCover, $"series{series.Id}.png", EncodeFormat.WEBP, 320, 455);
        Link(series, row);
        await context.SaveChangesAsync();
        var bytes = await File.ReadAllBytesAsync(Path.Join(_covers, row.FileName));

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        context.ChangeTracker.Clear();
        var newName = $"series{series.Id}.webp";
        var saved = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
        Assert.Equal(newName, saved.FileName);
        Assert.Equal(EncodeFormat.WEBP, saved.Format);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Join(_covers, newName)));
        Assert.False(File.Exists(Path.Join(_covers, $"series{series.Id}.png")));
        Assert.Equal(row.Id, (await context.Series.SingleAsync(s => s.Id == series.Id)).CoverImageFileId);
    }

    [Fact]
    public async Task FirstPass_MissingAndUnreadable_SkippedAndCounted()
    {
        var (service, context, eventHub, _, logger) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var person = new PersonBuilder("Jun Mochizuki").Build();
        context.Person.Add(person);
        await context.SaveChangesAsync();

        var missing = new ImageFile { Kind = ImageKind.SeriesCover, FileName = $"series{series.Id}.webp", Origin = ImageOrigin.Legacy };
        context.ImageFile.Add(missing);
        Link(series, missing);
        var unreadable = new ImageFile { Kind = ImageKind.PersonCover, FileName = $"person{person.Id}.webp", Origin = ImageOrigin.Legacy };
        context.ImageFile.Add(unreadable);
        Link(person, unreadable);
        await context.SaveChangesAsync();
        var unreadablePath = Path.Join(_covers, unreadable.FileName);
        await File.WriteAllTextAsync(unreadablePath, "not an image");

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        context.ChangeTracker.Clear();
        Assert.Null((await context.ImageFile.SingleAsync(f => f.Id == missing.Id)).Format);
        Assert.Null((await context.ImageFile.SingleAsync(f => f.Id == unreadable.Id)).Format);
        Assert.True(File.Exists(unreadablePath));

        var summary = SummaryOf(eventHub);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.Missing);
        Assert.Equal(1, summary.Unreadable);
        AssertLoggedInformation(logger, Path.Join(_covers, missing.FileName));
        AssertLoggedInformation(logger, unreadablePath);
    }

    [Fact]
    public async Task FirstPass_Summary_CountsAndBytes()
    {
        var (service, context, eventHub, _, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var chapter = series.Volumes[0].Chapters[0];
        Link(series, AddUnprobedRow(context, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.WEBP, 1280, 1820));
        Link(chapter, AddUnprobedRow(context, ImageKind.ChapterCover, $"v{series.Volumes[0].Id}_c{chapter.Id}.webp", EncodeFormat.PNG, 320, 455));
        await context.SaveChangesAsync();
        WriteImage(Path.Join(_covers, "v9_c9.webp"), EncodeFormat.WEBP, 320, 455);
        var sizeBefore = FolderSize(_covers);

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        var summary = SummaryOf(eventHub);
        Assert.NotNull(summary);
        Assert.Equal(2, summary.Corrected);
        Assert.Equal(1, summary.Deleted);
        Assert.Equal(sizeBefore - FolderSize(_covers), summary.BytesSaved);
    }

    [Fact]
    public async Task FirstPass_DuplicateFaviconKey_KeepsOne()
    {
        var (service, context, eventHub, _, _) = await Setup(EncodeFormat.WEBP);
        WriteImage(Path.Join(_favicons, "anilist.co.png"), EncodeFormat.PNG, 32, 32);
        WriteImage(Path.Join(_favicons, "anilist.co.webp"), EncodeFormat.WEBP, 32, 32);
        var png = new ImageFile { Kind = ImageKind.Favicon, Key = "anilist.co", FileName = "anilist.co.png", Origin = ImageOrigin.Legacy };
        var webp = new ImageFile { Kind = ImageKind.Favicon, Key = "anilist.co", FileName = "anilist.co.webp", Origin = ImageOrigin.Legacy };
        context.ImageFile.AddRange(png, webp);
        await context.SaveChangesAsync();

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));

        context.ChangeTracker.Clear();
        var saved = await context.ImageFile.SingleAsync(f => f.Kind == ImageKind.Favicon);
        Assert.Equal(webp.Id, saved.Id);
        Assert.Equal(EncodeFormat.WEBP, saved.Format);
        Assert.True(File.Exists(Path.Join(_favicons, "anilist.co.webp")));
        Assert.False(File.Exists(Path.Join(_favicons, "anilist.co.png")));
        Assert.NotNull(SummaryOf(eventHub));
    }

    [Fact]
    public async Task FirstPass_FileWithNoRow_OnlyOldDeleted()
    {
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        Link(series, AddUnprobedRow(context, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.WEBP, 320, 455));
        await context.SaveChangesAsync();

        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var oldCover = Path.Join(_covers, "v9_c9.webp");
        var oldTmp = Path.Join(_covers, "v8_c8.0123.tmp");
        var oldFavicon = Path.Join(_favicons, "old.example.webp");
        var oldBookmark = Path.Join(_bookmarks, "old.png");
        foreach (var path in new[] { oldCover, oldTmp, oldFavicon, oldBookmark })
        {
            WriteImage(path, EncodeFormat.WEBP, 32, 32);
            File.SetLastWriteTimeUtc(path, cutoff.AddHours(-1));
        }
        var newCover = Path.Join(_covers, "v7_c7.webp");
        var newTmp = Path.Join(_covers, "v6_c6.0123.tmp");
        WriteImage(newCover, EncodeFormat.WEBP, 32, 32);
        WriteImage(newTmp, EncodeFormat.WEBP, 32, 32);

        await service.FinishImageFileMigration(cutoff);

        Assert.False(File.Exists(oldCover));
        Assert.False(File.Exists(oldTmp));
        Assert.False(File.Exists(oldFavicon));
        Assert.True(File.Exists(oldBookmark));
        Assert.True(File.Exists(newCover));
        Assert.True(File.Exists(newTmp));
    }

    [Fact]
    public async Task Cleanup_AfterFirstPass_WritesNothing()
    {
        var (service, context, _, _, _) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var chapter = series.Volumes[0].Chapters[0];
        var person = new PersonBuilder("Jun Mochizuki").Build();
        context.Person.Add(person);
        await context.SaveChangesAsync();
        Link(series, AddUnprobedRow(context, ImageKind.SeriesCover, $"series{series.Id}.webp", EncodeFormat.WEBP, 1280, 1820));
        Link(chapter, AddUnprobedRow(context, ImageKind.ChapterCover, $"v{series.Volumes[0].Id}_c{chapter.Id}.webp", EncodeFormat.PNG, 320, 455));
        Link(person, AddUnprobedRow(context, ImageKind.PersonCover, $"person{person.Id}.png", EncodeFormat.WEBP, 320, 455));
        await context.SaveChangesAsync();

        await service.FinishImageFileMigration(DateTime.UtcNow.AddMinutes(1));
        var before = Directory.GetFiles(_covers).ToDictionary(f => f, File.GetLastWriteTimeUtc);

        await service.CleanupImages(false);

        var after = Directory.GetFiles(_covers).ToDictionary(f => f, File.GetLastWriteTimeUtc);
        Assert.Equal(before, after);
    }

    private async Task<ImageTestSetup> Setup(EncodeFormat format)
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        (await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.EncodeMediaAs)).Value = format.ToString();
        (await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.CoverImageSize)).Value = nameof(CoverImageSize.Default);
        (await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.BookmarkDirectory)).Value = _bookmarks;
        await context.SaveChangesAsync();

        var directoryService = DirectoryServiceSubstitute.OnDisk(_covers, _favicons, _publishers);
        var pipeline = new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>());
        var imageFileService = new ImageFileService(unitOfWork, pipeline, directoryService, Substitute.For<IEventHub>(),
            Substitute.For<ILogger<ImageFileService>>());
        var eventHub = Substitute.For<IEventHub>();
        var backup = Substitute.For<IBackupService>();
        backup.BackupDatabase(Arg.Any<CancellationToken>()).Returns(true);
        var logger = Substitute.For<ILogger<CleanupService>>();
        var service = new CleanupService(logger, unitOfWork, eventHub, directoryService, pipeline, imageFileService, backup);

        return new ImageTestSetup(service, context, eventHub, backup, logger);
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
        WriteImage(path, format, width, height);

        var row = new ImageFile { Kind = kind, FileName = fileName, Origin = ImageOrigin.Scan, Format = format, Width = width, Height = height };
        context.ImageFile.Add(row);
        return row;
    }

    /// <summary>
    /// A row as the backfill left it: no format or size, and a file whose content may not match its extension
    /// </summary>
    private ImageFile AddUnprobedRow(DataContext context, ImageKind kind, string fileName, EncodeFormat content, int width, int height)
    {
        WriteImage(Path.Join(_covers, fileName), content, width, height);

        var row = new ImageFile { Kind = kind, FileName = fileName, Origin = ImageOrigin.Legacy };
        context.ImageFile.Add(row);
        return row;
    }

    private static void WriteImage(string path, EncodeFormat format, int width, int height)
    {
        using var black = Image.Black(width, height, bands: 3);
        using var colored = black + new double[] { 200, 80, 40 };
        using var image = colored.Cast(NetVips.Enums.BandFormat.Uchar);
        if (format == EncodeFormat.PNG)
        {
            image.Pngsave(path);
        }
        else
        {
            image.Webpsave(path);
        }
    }

    private static long FolderSize(string directory) => Directory.GetFiles(directory).Sum(f => new FileInfo(f).Length);

    private static async Task<bool> HasMigrationHistory(DataContext context) =>
        await context.ManualMigrationHistory.AnyAsync(m => m.Name == CleanupService.ImageFileMigrationName);

    private static ImageCleanupSummaryEventBodyDto? SummaryOf(IEventHub eventHub, string code = MessageEventCode.ImageMigrationSummary) =>
        eventHub.ReceivedCalls()
            .SelectMany(c => c.GetArguments())
            .OfType<SignalRMessageDto>()
            .Where(m => m.Code == code)
            .Select(m => (ImageCleanupSummaryEventBodyDto) m.Body!)
            .SingleOrDefault();

    private static void AssertLoggedInformation(ILogger logger, string text)
    {
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(text)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }


    private static void Link(IHasCoverImageFile entity, ImageFile row)
    {
        entity.CoverImageFile = row;
    }

    private sealed record ImageTestSetup(CleanupService Service, DataContext Context, IEventHub EventHub,
        IBackupService Backup, ILogger<CleanupService> Logger);
}
