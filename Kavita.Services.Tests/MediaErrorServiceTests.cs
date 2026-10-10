using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class MediaErrorServiceTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    private static readonly string Books = Root + "books/";
    private static readonly string Murderbot = Books + "The Murderbot Diaries/";
    private static readonly DateTime WriteTime = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static MockFileSystem FileSystemWith(string path, int bytes = 10, DateTime? writeTime = null)
    {
        var fs = new MockFileSystem();
        fs.AddFile(path, new MockFileData(new byte[bytes]));
        fs.File.SetLastWriteTimeUtc(path, writeTime ?? WriteTime);
        return fs;
    }

    private static DirectoryService DirectoryServiceFor(MockFileSystem fs)
    {
        return new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
    }

    private static Library BooksLibrary(params Series[] series)
    {
        var builder = new LibraryBuilder("Books").WithFolderPath(new FolderPathBuilder(Books).Build());
        foreach (var s in series)
        {
            builder.WithSeries(s);
        }
        return builder.Build();
    }

    private static Series SeriesWithFile(string name, string filePath)
    {
        return new SeriesBuilder(name)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder(filePath, MangaFormat.Epub).Build())
                    .Build())
                .Build())
            .Build();
    }

    [Fact]
    public async Task ReportMediaIssueAsync_StoresFullPathStampAndOwner()
    {
        var path = Murderbot + "Fugitive Telemetry.epub";
        var (unitOfWork, context, _) = await CreateDatabase();
        var series = SeriesWithFile("The Murderbot Diaries", path);
        var library = BooksLibrary(series);
        context.Library.Add(library);
        await context.SaveChangesAsync();

        var service = new MediaErrorService(unitOfWork, DirectoryServiceFor(FileSystemWith(path)));
        await service.ReportMediaIssueAsync(path.Replace('/', '\\'), MediaErrorProducer.BookService, MediaErrorReason.CoverFailed,
            new InvalidOperationException("No cover", new Exception("Bad image")));

        var row = Assert.Single(await context.MediaError.AsNoTracking().ToListAsync());
        Assert.Equal(path, row.FilePath);
        Assert.Equal(10, row.Bytes);
        Assert.Equal(WriteTime, row.FileLastWriteTimeUtc);
        Assert.Equal(library.Id, row.LibraryId);
        Assert.Equal(series.Id, row.SeriesId);
        Assert.Equal(MediaErrorReason.CoverFailed, row.Reason);
        Assert.Equal(MediaErrorProducer.BookService, row.Producer);
        Assert.Equal("No cover -> Bad image", row.Details);
    }

    [Fact]
    public async Task ReportMediaIssueAsync_NoSeriesHasTheFileYet_UsesTheLibraryFolder()
    {
        var path = Murderbot + "Fugitive Telemetry.epub";
        var (unitOfWork, context, _) = await CreateDatabase();
        var library = BooksLibrary();
        context.Library.Add(library);
        context.Library.Add(new LibraryBuilder("Other").WithFolderPath(new FolderPathBuilder(Root + "other/").Build()).Build());
        await context.SaveChangesAsync();

        var service = new MediaErrorService(unitOfWork, DirectoryServiceFor(FileSystemWith(path)));
        await service.ReportMediaIssueAsync(path, MediaErrorProducer.ArchiveService, MediaErrorReason.UnreadableArchive, "details");

        var row = Assert.Single(await context.MediaError.AsNoTracking().ToListAsync());
        Assert.Equal(library.Id, row.LibraryId);
        Assert.Null(row.SeriesId);
    }

    [Fact]
    public async Task ReportMediaIssueAsync_SameFailureOnUnchangedFile_WritesNothing()
    {
        var path = Murderbot + "Fugitive Telemetry.epub";
        var (unitOfWork, context, _) = await CreateDatabase();
        context.Library.Add(BooksLibrary());
        await context.SaveChangesAsync();

        var service = new MediaErrorService(unitOfWork, DirectoryServiceFor(FileSystemWith(path)));
        await service.ReportMediaIssueAsync(path, MediaErrorProducer.BookService, MediaErrorReason.NoPages, "first");
        var row = await context.MediaError.SingleAsync();
        row.IsDismissed = true;
        row.LastSeenUtc = DateTime.UtcNow.AddDays(-1);
        await context.SaveChangesAsync();

        await service.ReportMediaIssueAsync(path, MediaErrorProducer.BookService, MediaErrorReason.NoPages, "second");

        row = Assert.Single(await context.MediaError.AsNoTracking().ToListAsync());
        Assert.True(row.IsDismissed);
        Assert.Equal("first", row.Details);
        Assert.True(row.LastSeenUtc < DateTime.UtcNow.AddHours(-1));
    }

    [Fact]
    public async Task ReportMediaIssueAsync_SameFailureAfterTheSeriesIsSaved_PicksUpTheSeries()
    {
        var path = Murderbot + "Fugitive Telemetry.epub";
        var (unitOfWork, context, _) = await CreateDatabase();
        var library = BooksLibrary();
        context.Library.Add(library);
        await context.SaveChangesAsync();

        var service = new MediaErrorService(unitOfWork, DirectoryServiceFor(FileSystemWith(path)));
        await service.ReportMediaIssueAsync(path, MediaErrorProducer.ArchiveService, MediaErrorReason.UnreadableArchive, "first");

        var series = SeriesWithFile("The Murderbot Diaries", path);
        library.Series.Add(series);
        await context.SaveChangesAsync();

        await service.ReportMediaIssueAsync(path, MediaErrorProducer.ArchiveService, MediaErrorReason.UnreadableArchive, "second");

        var row = Assert.Single(await context.MediaError.AsNoTracking().ToListAsync());
        Assert.Equal(series.Id, row.SeriesId);
        Assert.Equal("first", row.Details);
    }

    [Fact]
    public async Task ReportMediaIssueAsync_SameFailureOnChangedFile_Undismisses()
    {
        var path = Murderbot + "Fugitive Telemetry.epub";
        var (unitOfWork, context, _) = await CreateDatabase();
        context.Library.Add(BooksLibrary());
        await context.SaveChangesAsync();

        await new MediaErrorService(unitOfWork, DirectoryServiceFor(FileSystemWith(path)))
            .ReportMediaIssueAsync(path, MediaErrorProducer.BookService, MediaErrorReason.NoPages, "first");
        var row = await context.MediaError.SingleAsync();
        row.IsDismissed = true;
        await context.SaveChangesAsync();

        await new MediaErrorService(unitOfWork, DirectoryServiceFor(FileSystemWith(path, 20)))
            .ReportMediaIssueAsync(path, MediaErrorProducer.BookService, MediaErrorReason.NoPages, "second");

        row = Assert.Single(await context.MediaError.AsNoTracking().ToListAsync());
        Assert.False(row.IsDismissed);
        Assert.Equal(20, row.Bytes);
    }

    [Fact]
    public async Task ReportMediaIssueAsync_LeavesScannerRowAlone()
    {
        var path = Murderbot + "Fugitive Telemetry.epub";
        var (unitOfWork, context, _) = await CreateDatabase();
        var library = BooksLibrary();
        context.Library.Add(library);
        await context.SaveChangesAsync();

        var scannerRow = new MediaErrorBuilder(path).WithProducer(MediaErrorProducer.Scanner).WithReason(MediaErrorReason.CorruptEpub)
            .WithDetails("scanner").Build();
        scannerRow.LibraryId = library.Id;
        context.MediaError.Add(scannerRow);
        await context.SaveChangesAsync();

        await new MediaErrorService(unitOfWork, DirectoryServiceFor(FileSystemWith(path)))
            .ReportMediaIssueAsync(path, MediaErrorProducer.BookService, MediaErrorReason.CorruptEpub, "reader");

        var rows = await context.MediaError.AsNoTracking().OrderBy(m => m.Producer).ToListAsync();
        Assert.Equal([MediaErrorProducer.BookService, MediaErrorProducer.Scanner], rows.Select(r => r.Producer));
        Assert.Equal("scanner", rows[1].Details);
    }

    [Fact]
    public async Task ReportMediaIssueAsync_EmptyPath_WritesNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        await new MediaErrorService(unitOfWork, DirectoryServiceFor(new MockFileSystem()))
            .ReportMediaIssueAsync(string.Empty, MediaErrorProducer.BookService, MediaErrorReason.CorruptEpub, "details");

        Assert.Empty(await context.MediaError.ToListAsync());
    }
}
