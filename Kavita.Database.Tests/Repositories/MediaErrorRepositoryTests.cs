using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;
using Kavita.Services.Builders;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Database.Tests.Repositories;

public class MediaErrorRepositoryTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    private const string Murderbot = "B:/Fiction/Martha Wells/The Murderbot Diaries";
    private static readonly DateTime WriteTime = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static MediaError Row(string path, int? libraryId, long? bytes = 100, DateTime? writeTime = null,
        MediaErrorProducer producer = MediaErrorProducer.Scanner, MediaErrorReason reason = MediaErrorReason.CorruptEpub,
        bool isDismissed = false)
    {
        var error = new MediaErrorBuilder(path).WithProducer(producer).WithDetails("details").Build();
        error.LibraryId = libraryId;
        error.Bytes = bytes;
        error.FileLastWriteTimeUtc = writeTime ?? (bytes == null ? null : WriteTime);
        error.Reason = reason;
        error.IsDismissed = isDismissed;
        return error;
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
    public async Task GetFailedFilesAsync_ReturnsStampedScannerRowsOfTheLibraryOnly()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var books = new LibraryBuilder("Books").Build();
        var manga = new LibraryBuilder("Manga").Build();
        context.Library.AddRange(books, manga);
        await context.SaveChangesAsync();

        context.MediaError.AddRange(
            Row($"{Murderbot}/Fugitive Telemetry.epub", books.Id, 100, WriteTime),
            Row($"{Murderbot}/Dismissed.epub", books.Id, 200, WriteTime, isDismissed: true),
            Row($"{Murderbot}/No Stamp.epub", books.Id, null),
            Row($"{Murderbot}/Cover Failed In Reader.epub", books.Id, 300, WriteTime, MediaErrorProducer.BookService),
            Row("Other Producer.epub", null, 300, WriteTime),
            Row("M:/Manga/Other Library.cbz", manga.Id, 400, WriteTime));
        await context.SaveChangesAsync();

        var failed = await unitOfWork.MediaErrorRepository.GetFailedFilesAsync(books.Id);

        Assert.Equal(
        [
            new FailedFile($"{Murderbot}/Dismissed.epub", 200, WriteTime),
            new FailedFile($"{Murderbot}/Fugitive Telemetry.epub", 100, WriteTime),
        ], failed.OrderBy(f => f.Path));
    }

    [Fact]
    public async Task AssignScannerErrorsToSeriesAsync_GivesTheFilesOwnSeriesElseTheOnlySeriesInTheFolder()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var murderbot = SeriesWithFile("The Murderbot Diaries", $"{Murderbot}/All Systems Red.epub");
        var novella = SeriesWithFile("Novellas", "B:/Fiction/Shared/Novella.epub");
        var other = SeriesWithFile("Other", "B:/Fiction/Shared/Other.epub");
        var nested = SeriesWithFile("Nested", $"{Murderbot}/Extras/Extra.epub");
        var books = new LibraryBuilder("Books").WithSeries(murderbot).WithSeries(novella).WithSeries(other).WithSeries(nested).Build();
        context.Library.Add(books);
        await context.SaveChangesAsync();

        var rows = new[]
        {
            Row($"{Murderbot}/Fugitive Telemetry.epub", books.Id),
            Row($"{Murderbot}/Not Saved This Scan.epub", books.Id),
            Row("B:/Fiction/Shared/Broken.epub", books.Id),
            Row("B:/Fiction/Shared/Novella.epub", books.Id, reason: MediaErrorReason.EpubNotStrict),
            Row("B:/Fiction/Empty/Broken.epub", books.Id),
            Row($"{Murderbot}/Reader Failure.epub", books.Id, producer: MediaErrorProducer.BookService),
        };
        context.MediaError.AddRange(rows);
        await context.SaveChangesAsync();

        var filesByFolder = new Dictionary<string, IList<string>>
        {
            [Murderbot] = [$"{Murderbot}/All Systems Red.epub", $"{Murderbot}/Fugitive Telemetry.epub", $"{Murderbot}/Not Saved This Scan.epub"],
            ["B:/Fiction/Shared"] = ["B:/Fiction/Shared/Novella.epub", "B:/Fiction/Shared/Other.epub", "B:/Fiction/Shared/Broken.epub"],
            ["B:/Fiction/Empty"] = ["B:/Fiction/Empty/Broken.epub"],
        };
        await unitOfWork.MediaErrorRepository.AssignScannerErrorsToSeriesAsync(books.Id,
            rows.Select(r => r.FilePath).Where(p => !p.Contains("Not Saved")).ToList(), filesByFolder);
        await unitOfWork.CommitAsync();

        var seriesByPath = await context.MediaError.AsNoTracking().ToDictionaryAsync(m => m.FilePath, m => m.SeriesId);
        Assert.Equal(murderbot.Id, seriesByPath[$"{Murderbot}/Fugitive Telemetry.epub"]);
        Assert.Null(seriesByPath[$"{Murderbot}/Not Saved This Scan.epub"]);
        Assert.Null(seriesByPath["B:/Fiction/Shared/Broken.epub"]);
        Assert.Equal(novella.Id, seriesByPath["B:/Fiction/Shared/Novella.epub"]);
        Assert.Null(seriesByPath["B:/Fiction/Empty/Broken.epub"]);
        Assert.Null(seriesByPath[$"{Murderbot}/Reader Failure.epub"]);
    }

    [Fact]
    public async Task GetAllErrorDtosAsync_IncludesDismissedWithLibraryAndSeriesNames()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var murderbot = SeriesWithFile("The Murderbot Diaries", $"{Murderbot}/All Systems Red.epub");
        var books = new LibraryBuilder("Books").WithSeries(murderbot).Build();
        context.Library.Add(books);
        await context.SaveChangesAsync();

        var withSeries = Row($"{Murderbot}/Fugitive Telemetry.epub", books.Id);
        withSeries.SeriesId = murderbot.Id;
        context.MediaError.AddRange(
            withSeries,
            Row($"{Murderbot}/Dismissed.epub", books.Id, isDismissed: true),
            Row("Orphan.epub", null));
        await context.SaveChangesAsync();

        var dtos = (await unitOfWork.MediaErrorRepository.GetAllErrorDtosAsync()).ToDictionary(d => d.FilePath);

        Assert.Equal(3, dtos.Count);
        Assert.Equal(("Books", "The Murderbot Diaries", false),
            (dtos[$"{Murderbot}/Fugitive Telemetry.epub"].LibraryName, dtos[$"{Murderbot}/Fugitive Telemetry.epub"].SeriesName,
                dtos[$"{Murderbot}/Fugitive Telemetry.epub"].IsDismissed));
        Assert.True(dtos[$"{Murderbot}/Dismissed.epub"].IsDismissed);
        Assert.Null(dtos["Orphan.epub"].LibraryName);
        Assert.True(dtos.Values.All(d => d.Id > 0));
    }

    [Fact]
    public async Task GetUnreadableFiles_LeavesOutDismissedImportedAndOtherProducers()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var murderbot = SeriesWithFile("The Murderbot Diaries", $"{Murderbot}/All Systems Red.epub");
        var books = new LibraryBuilder("Books").WithSeries(murderbot).Build();
        context.Library.Add(books);
        await context.SaveChangesAsync();

        var older = Row($"{Murderbot}/Older.epub", books.Id);
        older.LastSeenUtc = DateTime.UtcNow.AddDays(-1);
        var newer = Row($"{Murderbot}/Fugitive Telemetry.epub", books.Id);
        newer.SeriesId = murderbot.Id;
        context.MediaError.AddRange(
            older,
            newer,
            Row($"{Murderbot}/Dismissed.epub", books.Id, isDismissed: true),
            Row($"{Murderbot}/Lenient.epub", books.Id, reason: MediaErrorReason.EpubNotStrict),
            Row($"{Murderbot}/No ComicInfo.epub", books.Id, reason: MediaErrorReason.MetadataUnreadable),
            Row($"{Murderbot}/Reader.epub", books.Id, producer: MediaErrorProducer.BookService));
        await context.SaveChangesAsync();

        Assert.Equal(2, await unitOfWork.MediaErrorRepository.GetUnreadableFileCountAsync(books.Id));

        var files = await unitOfWork.MediaErrorRepository.GetUnreadableFilesAsync(books.Id, 1);
        var file = Assert.Single(files);
        Assert.Equal(($"{Murderbot}/Fugitive Telemetry.epub", "The Murderbot Diaries"), (file.FilePath, file.SeriesName));
    }
}
