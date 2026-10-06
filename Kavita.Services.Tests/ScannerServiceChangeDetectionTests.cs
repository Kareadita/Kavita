using System.Collections.Concurrent;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using System.IO.Abstractions;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class ScannerServiceChangeDetectionTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    public static TheoryData<string, string[]> UnchangedLayouts => new()
    {
        {
            "NoChange Flat - Manga",
            [
                "Spice and Wolf/Spice and Wolf Vol. 1.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 2.cbz",
                "Accel World/Accel World Vol. 1.cbz",
            ]
        },
        {
            "NoChange Publisher - Manga",
            [
                "VizMedia/Frieren - Beyond Journey's End/Frieren - Beyond Journey's End Vol. 1.cbz",
                "VizMedia/Seraph of the End/Seraph of the End Vol. 1.cbz",
                "YenPress/Spice and Wolf/Spice and Wolf Vol. 1.cbz",
            ]
        },
    };

    [Theory]
    [MemberData(nameof(UnchangedLayouts))]
    public async Task NoChange_SecondScanProcessesNothing(string testcase, string[] files)
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, testcase, files);

        var before = await LastFolderScanned(context, libraryId);
        await scanner.ScanLibrary(libraryId);

        Assert.Equal(before, await LastFolderScanned(context, libraryId));
    }

    [Fact]
    public async Task NoChange_VolumeFolders_SecondScanProcessesNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "NoChange Volume Folders - Manga",
        [
            "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
            "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0003.cbz",
        ]);

        var before = await LastFolderScanned(context, libraryId);
        await scanner.ScanLibrary(libraryId);

        Assert.Equal(before, await LastFolderScanned(context, libraryId));
    }

    [Fact]
    public async Task Scan_StoresEachFilesOwnWriteTime()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Stores Write Time - Manga",
        [
            "Spice and Wolf/Spice and Wolf Vol. 1.cbz",
            "Spice and Wolf/Spice and Wolf Vol. 2.cbz",
        ]);
        await AssertStoredWriteTimesMatchDisk(context, libraryId);

        var root = await LibraryRoot(context, libraryId);
        File.SetLastWriteTimeUtc(Path.Join(root, "Spice and Wolf", "Spice and Wolf Vol. 2.cbz"), DateTime.UtcNow.AddMinutes(5));
        await scanner.ScanLibrary(libraryId);

        await AssertStoredWriteTimesMatchDisk(context, libraryId);
    }

    private static readonly string[] TwoVolumes =
    [
        "Spice and Wolf/Spice and Wolf Vol. 1.cbz",
        "Spice and Wolf/Spice and Wolf Vol. 2.cbz",
    ];

    [Fact]
    public async Task CopyOverWithOlderTimeAndOtherSize_IsPickedUp()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Copy Over Older - Manga", TwoVolumes);
        var before = await LastFolderScanned(context, libraryId);

        var root = await LibraryRoot(context, libraryId);
        var seriesFolder = Path.Join(root, "Spice and Wolf");
        var folderTime = Directory.GetLastWriteTimeUtc(seriesFolder);
        var volume2 = Path.Join(seriesFolder, "Spice and Wolf Vol. 2.cbz");
        await File.WriteAllBytesAsync(volume2, new byte[1200]);
        File.SetLastWriteTimeUtc(volume2, DateTime.UtcNow.AddYears(-4));
        Directory.SetLastWriteTimeUtc(seriesFolder, folderTime);

        await scanner.ScanLibrary(libraryId);

        Assert.NotEqual(before, await LastFolderScanned(context, libraryId));
    }

    private static readonly Dictionary<string, ComicInfo> TwoVolumeComicInfos = new()
    {
        {"Spice and Wolf Vol. 1.cbz", new ComicInfo {Series = "Spice and Wolf", Volume = "1"}},
        {"Spice and Wolf Vol. 2.cbz", new ComicInfo {Series = "Spice and Wolf", Volume = "2"}},
    };

    [Fact]
    public async Task CopyOverWithOlderTimeAndMorePages_IsReReadOnceThenQuiet()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Copy Over Re-Read - Manga", TwoVolumes, TwoVolumeComicInfos);

        var root = await LibraryRoot(context, libraryId);
        var volume2 = Path.Join(root, "Spice and Wolf", "Spice and Wolf Vol. 2.cbz");
        await using (var archive = await ZipFile.OpenAsync(volume2, ZipArchiveMode.Update))
        {
            using var page = new MemoryStream();
            await using (var source = await archive.Entries.First(e => e.Name.EndsWith(".png")).OpenAsync())
            {
                await source.CopyToAsync(page);
            }
            await using var target = await archive.CreateEntry("2.png").OpenAsync();
            await target.WriteAsync(page.ToArray());
        }
        File.SetLastWriteTimeUtc(volume2, DateTime.UtcNow.AddYears(-4));

        await scanner.ScanLibrary(libraryId);

        var file = await context.MangaFile.AsNoTracking().SingleAsync(f => f.FilePath == Parser.NormalizePath(volume2));
        Assert.Equal(2, file.Pages);
        Assert.Equal(new FileInfo(volume2).Length, file.Bytes);
        await AssertStoredWriteTimesMatchDisk(context, libraryId);

        var afterReRead = await LastFolderScanned(context, libraryId);
        await scanner.ScanLibrary(libraryId);
        Assert.Equal(afterReRead, await LastFolderScanned(context, libraryId));
    }

    [Fact]
    public async Task CopyOverWithOlderTime_NewComicInfoIsApplied()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var comicInfos = new Dictionary<string, ComicInfo>
        {
            {"Spice and Wolf Vol. 1.cbz", new ComicInfo {Series = "Spice and Wolf", Volume = "1"}},
            {"Spice and Wolf Vol. 2.cbz", new ComicInfo {Series = "Spice and Wolf", Volume = "2", Title = "Old Title"}},
        };
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Copy Over ComicInfo - Manga", TwoVolumes, comicInfos);

        var root = await LibraryRoot(context, libraryId);
        var volume2 = Path.Join(root, "Spice and Wolf", "Spice and Wolf Vol. 2.cbz");
        using (var archive = ZipFile.Open(volume2, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("ComicInfo.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open()))
            {
                xml = await reader.ReadToEndAsync();
            }
            entry.Delete();
            await using var writer = new StreamWriter(archive.CreateEntry("ComicInfo.xml").Open());
            await writer.WriteAsync(xml.Replace("Old Title", "A Much Longer New Title"));
        }
        File.SetLastWriteTimeUtc(volume2, DateTime.UtcNow.AddYears(-4));

        await scanner.ScanLibrary(libraryId);

        var titleName = await context.Chapter.AsNoTracking()
            .Where(c => c.Files.Any(f => f.FilePath == Parser.NormalizePath(volume2)))
            .Select(c => c.TitleName)
            .SingleAsync();
        Assert.Equal("A Much Longer New Title", titleName);
    }

    [Fact]
    public async Task SameFileWithOlderTime_IsReReadOnceThenQuiet()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Older Time Re-Read - Manga", TwoVolumes, TwoVolumeComicInfos);

        var root = await LibraryRoot(context, libraryId);
        File.SetLastWriteTimeUtc(Path.Join(root, "Spice and Wolf", "Spice and Wolf Vol. 2.cbz"), DateTime.UtcNow.AddYears(-4));

        await scanner.ScanLibrary(libraryId);
        await AssertStoredWriteTimesMatchDisk(context, libraryId);

        var afterReRead = await LastFolderScanned(context, libraryId);
        await scanner.ScanLibrary(libraryId);
        Assert.Equal(afterReRead, await LastFolderScanned(context, libraryId));
    }

    [Fact]
    public async Task NewFileWithOldTime_IsPickedUp()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "New File Old Time - Manga", TwoVolumes);

        var root = await LibraryRoot(context, libraryId);
        var seriesFolder = Path.Join(root, "Spice and Wolf");
        var folderTime = Directory.GetLastWriteTimeUtc(seriesFolder);
        await scannerHelper.Scaffold(root, ["Spice and Wolf/Spice and Wolf Vol. 3.cbz"]);
        File.SetLastWriteTimeUtc(Path.Join(seriesFolder, "Spice and Wolf Vol. 3.cbz"), DateTime.UtcNow.AddYears(-5));
        Directory.SetLastWriteTimeUtc(seriesFolder, folderTime);

        await scanner.ScanLibrary(libraryId);

        var seriesId = await SeriesId(context, libraryId, "Spice and Wolf");
        Assert.Equal(3, await SeriesFileCount(context, seriesId));
    }

    [Fact]
    public async Task NoStoredWriteTimes_SecondScanProcessesNothingAndFillsThem()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Backfill Write Times - Manga", TwoVolumes);
        await context.MangaFile
            .Where(f => f.Chapter.Volume.Series.LibraryId == libraryId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.FileLastWriteTimeUtc, (DateTime?) null));
        var before = await LastFolderScanned(context, libraryId);

        await scanner.ScanLibrary(libraryId);

        Assert.Equal(before, await LastFolderScanned(context, libraryId));
        await AssertStoredWriteTimesMatchDisk(context, libraryId);
    }

    [Fact]
    public async Task NonLibraryFileInFolder_SecondScanProcessesNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Non Library File - Manga", TwoVolumes);
        var root = await LibraryRoot(context, libraryId);
        await File.WriteAllTextAsync(Path.Join(root, "Spice and Wolf", "notes.txt"), "not a volume");
        var before = await LastFolderScanned(context, libraryId);

        await scanner.ScanLibrary(libraryId);

        Assert.Equal(before, await LastFolderScanned(context, libraryId));
    }

    [Fact]
    public async Task CoverImageInSeriesFolder_SecondScanProcessesNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Cover Image - Manga", [.. TwoVolumes, "Spice and Wolf/cover.jpg"]);
        var before = await LastFolderScanned(context, libraryId);

        await scanner.ScanLibrary(libraryId);

        Assert.Equal(before, await LastFolderScanned(context, libraryId));
    }

    private static async Task AssertStoredWriteTimesMatchDisk(DataContext context, int libraryId)
    {
        var files = await context.MangaFile.AsNoTracking()
            .Where(f => f.Chapter.Volume.Series.LibraryId == libraryId)
            .Select(f => new { f.FilePath, f.FileLastWriteTimeUtc })
            .ToListAsync();

        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.Equal(File.GetLastWriteTimeUtc(f.FilePath), f.FileLastWriteTimeUtc));
    }

    private static readonly string[] SpecialsBesideVolumes =
    [
        "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
        "Spice and Wolf/Specials/Spice and Wolf SP01.cbz",
    ];

    [Fact]
    public async Task SpecialsBesideVolumeFolders_AreAdded()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (_, libraryId) = await ScanOnce(unitOfWork, "Specials Beside Volumes - Manga", SpecialsBesideVolumes);

        var series = await context.Series.AsNoTracking().SingleAsync(s => s.LibraryId == libraryId);
        Assert.Equal("Spice and Wolf", series.Name);
        Assert.Equal(["Spice and Wolf SP01.cbz", "Spice and Wolf Vol. 1 Ch. 0001.cbz"], await SeriesFileNames(context, series.Id));
    }

    [Fact]
    public async Task SpecialsBesideVolumeFolders_NoChange_SecondScanProcessesNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Specials Beside Volumes NoChange - Manga", SpecialsBesideVolumes);

        var before = await LastFolderScanned(context, libraryId);
        await scanner.ScanLibrary(libraryId);

        Assert.Equal(before, await LastFolderScanned(context, libraryId));
    }

    [Fact]
    public async Task SpecialsBesideVolumeFolders_VolumeFolderChanged_KeepsSpecials()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Specials Beside Volumes Volume Changed - Manga", SpecialsBesideVolumes);

        var root = await LibraryRoot(context, libraryId);
        await scannerHelper.Scaffold(root, ["Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Spice and Wolf", "Spice and Wolf Vol. 1"), DateTime.Now.AddSeconds(2));
        await scanner.ScanLibrary(libraryId);

        var seriesId = await context.Series.Where(s => s.LibraryId == libraryId).Select(s => s.Id).SingleAsync();
        Assert.Equal(
            ["Spice and Wolf SP01.cbz", "Spice and Wolf Vol. 1 Ch. 0001.cbz", "Spice and Wolf Vol. 1 Ch. 0002.cbz"],
            await SeriesFileNames(context, seriesId));
    }

    [Fact]
    public async Task SpecialsBesideVolumeFolders_SpecialAdded_IsRead()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Specials Beside Volumes Special Added - Manga", SpecialsBesideVolumes);

        var root = await LibraryRoot(context, libraryId);
        await scannerHelper.Scaffold(root, ["Spice and Wolf/Specials/Spice and Wolf SP02.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Spice and Wolf", "Specials"), DateTime.Now.AddSeconds(2));
        await scanner.ScanLibrary(libraryId);

        var seriesId = await context.Series.Where(s => s.LibraryId == libraryId).Select(s => s.Id).SingleAsync();
        Assert.Equal(
            ["Spice and Wolf SP01.cbz", "Spice and Wolf SP02.cbz", "Spice and Wolf Vol. 1 Ch. 0001.cbz"],
            await SeriesFileNames(context, seriesId));
    }

    private static readonly string[] TwoSeries =
    [
        "Accel World/Accel World Vol. 1.cbz",
        "Berserk/Berserk Vol. 1/Berserk Vol. 1 Ch. 0001.cbz",
        "Berserk/Berserk Vol. 2/Berserk Vol. 2 Ch. 0002.cbz",
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScanLibrary_UnreadableSeriesFolder_ScansTheRestAndKeepsIt(bool forceUpdate)
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (_, libraryId) = await ScanOnce(unitOfWork, $"Unreadable Series Folder {forceUpdate} - Manga", TwoSeries);
        var root = await LibraryRoot(context, libraryId);

        await scannerHelper.Scaffold(root, ["Accel World/Accel World Vol. 2.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Accel World"), DateTime.Now.AddSeconds(2));
        await ScannerWithUnreadable(unitOfWork, Path.Join(root, "Berserk")).ScanLibrary(libraryId, forceUpdate);

        Assert.Equal(["Accel World Vol. 1.cbz", "Accel World Vol. 2.cbz"],
            await SeriesFileNames(context, await SeriesId(context, libraryId, "Accel World")));
        Assert.Equal(["Berserk Vol. 1 Ch. 0001.cbz", "Berserk Vol. 2 Ch. 0002.cbz"],
            await SeriesFileNames(context, await SeriesId(context, libraryId, "Berserk")));
    }

    [Fact]
    public async Task ScanLibrary_UnreadableVolumeFolder_SeriesProcessedKeepsThatVolume()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (_, libraryId) = await ScanOnce(unitOfWork, "Unreadable Volume Folder - Manga", TwoSeries);
        var root = await LibraryRoot(context, libraryId);

        await scannerHelper.Scaffold(root, ["Berserk/Berserk Vol. 1/Berserk Vol. 1 Ch. 0003.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Berserk", "Berserk Vol. 1"), DateTime.Now.AddSeconds(2));
        await ScannerWithUnreadable(unitOfWork, Path.Join(root, "Berserk", "Berserk Vol. 2")).ScanLibrary(libraryId);

        Assert.Equal(["Berserk Vol. 1 Ch. 0001.cbz", "Berserk Vol. 1 Ch. 0003.cbz", "Berserk Vol. 2 Ch. 0002.cbz"],
            await SeriesFileNames(context, await SeriesId(context, libraryId, "Berserk")));
    }

    [Fact]
    public async Task ScanLibrary_UnreadableLibraryRoot_RemovesNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (_, libraryId) = await ScanOnce(unitOfWork, "Unreadable Library Root - Manga", TwoSeries);
        var root = await LibraryRoot(context, libraryId);

        try
        {
            await ScannerWithUnreadable(unitOfWork, root).ScanLibrary(libraryId);
        }
        catch (UnauthorizedAccessException)
        {
            // Aborting is fine, deleting is not
        }

        Assert.Equal(2, await context.Series.CountAsync(s => s.LibraryId == libraryId));
        Assert.Equal(3, await context.MangaFile.CountAsync(f => f.Chapter.Volume.Series.LibraryId == libraryId));
    }

    private ScannerService ScannerWithUnreadable(IUnitOfWork unitOfWork, params string[] folders)
    {
        var fs = UnreadableFolders.Wrap(new FileSystem(), folders);
        return new ScannerHelper(unitOfWork, testOutputHelper)
            .CreateServices(new DirectoryService(NullLogger<DirectoryService>.Instance, fs), fs);
    }

    private static Task<int> SeriesId(DataContext context, int libraryId, string name)
    {
        return context.Series.Where(s => s.LibraryId == libraryId && s.Name == name).Select(s => s.Id).SingleAsync();
    }

    private static async Task<List<string>> SeriesFileNames(DataContext context, int seriesId)
    {
        var paths = await context.MangaFile
            .Where(f => f.Chapter.Volume.SeriesId == seriesId)
            .Select(f => f.FilePath)
            .ToListAsync();

        return paths.Select(Path.GetFileName).Order().ToList()!;
    }

    /// <summary>
    /// A series with files in two top level folders has no common folder below the library root, so its
    /// LowestFolderPath is null and FolderPath names only the first folder
    /// </summary>
    [Fact]
    public async Task ScanSeries_FilesInTwoTopLevelFolders_KeepsBothFolders()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "ScanSeries Two Top Folders - Manga",
        [
            "Spice and Wolf/Spice and Wolf Vol. 1.cbz",
            "Spice and Wolf Extras/Spice and Wolf Vol. 2.cbz",
        ]);

        var series = await context.Series.AsNoTracking().SingleAsync(s => s.LibraryId == libraryId);
        Assert.Null(series.LowestFolderPath);

        var root = await LibraryRoot(context, libraryId);
        await scannerHelper.Scaffold(root, ["Spice and Wolf/Spice and Wolf Vol. 3.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Spice and Wolf"), DateTime.Now.AddSeconds(2));

        await scanner.ScanSeries(series.Id);

        var files = await context.MangaFile
            .Where(f => f.Chapter.Volume.SeriesId == series.Id)
            .Select(f => Path.GetFileName(f.FilePath))
            .ToListAsync();

        Assert.Equal(
            ["Spice and Wolf Vol. 1.cbz", "Spice and Wolf Vol. 2.cbz", "Spice and Wolf Vol. 3.cbz"],
            files.Order());
    }

    [Fact]
    public async Task ScanLibrary_FilesInTwoTopLevelFolders_DeletedFolderIsRemoved()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Delete One Of Two Top Folders - Manga",
        [
            "Spice and Wolf/Spice and Wolf Vol. 1.cbz",
            "Spice and Wolf Extras/Spice and Wolf Vol. 2.cbz",
        ]);

        var series = await context.Series.AsNoTracking().SingleAsync(s => s.LibraryId == libraryId);
        Assert.Null(series.LowestFolderPath);

        // Delete the folder that is not FolderPath, so the remaining FolderPath key is unchanged
        var root = await LibraryRoot(context, libraryId);
        var deleted = Path.GetFileName(series.FolderPath) == "Spice and Wolf" ? "Spice and Wolf Extras" : "Spice and Wolf";
        Directory.Delete(Path.Join(root, deleted), true);

        await scanner.ScanLibrary(libraryId);

        var files = await context.MangaFile
            .Where(f => f.Chapter.Volume.SeriesId == series.Id)
            .Select(f => f.FilePath)
            .ToListAsync();

        Assert.All(files, f => Assert.DoesNotContain($"/{deleted}/", f));
        Assert.Single(files);
    }

    [Fact]
    public async Task ScanLibrary_SeriesInTwoLibraryRoots_KeepsFilesFromBoth()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var library = await scannerHelper.GenerateScannerData("Series In Two Roots - Manga",
        [
            "Root 1/Spice and Wolf/Spice and Wolf Vol. 1.cbz",
            "Root 2/Spice and Wolf/Spice and Wolf Vol. 2.cbz",
        ], new Dictionary<string, ComicInfo>());

        var testDirectory = library.Folders.First().Path;
        library.Folders =
        [
            new FolderPath { Path = Path.Join(testDirectory, "Root 1") },
            new FolderPath { Path = Path.Join(testDirectory, "Root 2") },
        ];
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        await scannerHelper.CreateServices().ScanLibrary(library.Id);

        var files = await context.MangaFile
            .Where(f => f.Chapter.Volume.Series.LibraryId == library.Id)
            .Select(f => f.FilePath)
            .ToListAsync();

        Assert.Equal(["Spice and Wolf Vol. 1.cbz", "Spice and Wolf Vol. 2.cbz"], files.Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task Scan_LowestFolderPathNoLongerValid_IsCleared()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Stale Lowest Folder - Manga",
        [
            "Spice and Wolf/Spice and Wolf Vol. 1.cbz",
            "Spice and Wolf Extras/Spice and Wolf Vol. 2.cbz",
        ]);

        var root = await LibraryRoot(context, libraryId);
        await context.Series
            .Where(s => s.LibraryId == libraryId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LowestFolderPath, root + "/Spice and Wolf")
                .SetProperty(x => x.LastFolderScanned, DateTime.Now.AddHours(-2)));
        context.ChangeTracker.Clear();

        await scanner.ScanLibrary(libraryId);

        var series = await context.Series.AsNoTracking().SingleAsync(s => s.LibraryId == libraryId);
        Assert.Null(series.LowestFolderPath);
    }

    /// <summary>
    /// The series is processed with placeholders only, so nothing read from a file this scan
    /// </summary>
    [Fact]
    public async Task ScanLibrary_DeletedVolumeFolderOnly_KeepsNamesReadFromFiles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var comicInfo = new ComicInfo
        {
            Series = "Spice and Wolf",
            LocalizedSeries = "Ookami to Koushinryou",
            SeriesSort = "Wolf, Spice and",
        };
        var library = await scannerHelper.GenerateScannerData("Deleted Volume Keeps Names - Manga",
        [
            "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
            "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0003.cbz",
        ], new Dictionary<string, ComicInfo>
        {
            ["Spice and Wolf Vol. 1 Ch. 0001.cbz"] = comicInfo,
            ["Spice and Wolf Vol. 2 Ch. 0003.cbz"] = comicInfo,
        });
        var root = library.Folders.First().Path;
        ScannerHelper.Backdate(root, TimeSpan.FromHours(1));

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        var before = await context.Series.AsNoTracking().SingleAsync(s => s.LibraryId == library.Id);
        Assert.Equal("Ookami to Koushinryou", before.LocalizedName);
        Assert.Equal("Wolf, Spice and", before.SortName);

        Directory.Delete(Path.Join(root, "Spice and Wolf", "Spice and Wolf Vol. 2"), true);
        await scanner.ScanLibrary(library.Id);

        var after = await context.Series.AsNoTracking()
            .Include(s => s.Volumes)
            .SingleAsync(s => s.LibraryId == library.Id);
        Assert.Single(after.Volumes);
        Assert.Equal(before.LocalizedName, after.LocalizedName);
        Assert.Equal(before.SortName, after.SortName);
    }

    /// <summary>
    /// Placeholders and parsed files must land in one series even when the user renamed it
    /// </summary>
    [Fact]
    public async Task ScanLibrary_RenamedSeries_NewChapterKeepsUnchangedVolume()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (scanner, libraryId) = await ScanOnce(unitOfWork, "Renamed Series New Chapter - Manga",
        [
            "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
            "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0003.cbz",
        ]);

        const string customName = "Spice & Wolf (Custom)";
        await context.Series
            .Where(s => s.LibraryId == libraryId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Name, customName)
                .SetProperty(x => x.NormalizedName, customName.ToNormalized()));
        context.ChangeTracker.Clear();

        var root = await LibraryRoot(context, libraryId);
        await scannerHelper.Scaffold(root, ["Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0004.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Spice and Wolf", "Spice and Wolf Vol. 2"), DateTime.Now.AddSeconds(2));

        await scanner.ScanLibrary(libraryId);

        Assert.Single(await context.Series.AsNoTracking().Where(s => s.LibraryId == libraryId).ToListAsync());
        var files = await context.MangaFile
            .Where(f => f.Chapter.Volume.Series.LibraryId == libraryId)
            .Select(f => Path.GetFileName(f.FilePath))
            .ToListAsync();
        Assert.Equal(
            ["Spice and Wolf Vol. 1 Ch. 0001.cbz", "Spice and Wolf Vol. 2 Ch. 0003.cbz", "Spice and Wolf Vol. 2 Ch. 0004.cbz"],
            files.Order());
    }

    [Fact]
    public async Task ScanLibrary_FileAddedWhileScanning_IsReadOnNextScan()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var library = await scannerHelper.GenerateScannerData("File Added While Scanning - Manga",
            ["Spice and Wolf/Spice and Wolf Vol. 1.cbz"], new Dictionary<string, ComicInfo>());
        var seriesFolder = Path.Join(library.Folders.First().Path, "Spice and Wolf");
        ScannerHelper.Backdate(library.Folders.First().Path, TimeSpan.FromHours(1));

        var scanner = scannerHelper.CreateServices(wrapScanReader: reader => new RunOnFirstParse(reader, () =>
        {
            var added = Path.Join(seriesFolder, "Spice and Wolf Vol. 2.cbz");
            File.Copy(Path.Join(seriesFolder, "Spice and Wolf Vol. 1.cbz"), added);
            File.SetLastWriteTime(added, DateTime.Now);
        }));

        await scanner.ScanLibrary(library.Id);
        var seriesId = await context.Series.Where(s => s.LibraryId == library.Id).Select(s => s.Id).SingleAsync();
        Assert.Equal(1, await SeriesFileCount(context, seriesId));

        await scanner.ScanLibrary(library.Id);
        Assert.Equal(2, await SeriesFileCount(context, seriesId));
    }

    private sealed class RunOnFirstParse(IReadingItemService inner, Action onFirstParse) : IReadingItemService
    {
        private int _fired;

        public int GetNumberOfPages(string filePath, MangaFormat format) => inner.GetNumberOfPages(filePath, format);

        public string GetCoverImage(string filePath, string fileName, MangaFormat format, EncodeFormat encodeFormat,
            CoverImageSize size = CoverImageSize.Default) =>
            inner.GetCoverImage(filePath, fileName, format, encodeFormat, size);

        public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1) =>
            inner.Extract(fileFilePath, targetDirectory, format, imageCount);

        public ParseFileResult ParseFile(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata)
        {
            if (Interlocked.Exchange(ref _fired, 1) == 0) onFirstParse();
            return inner.ParseFile(path, rootPath, libraryRoot, type, enableMetadata);
        }
    }

    private const string LocalizedFolderFile = "Ookami to Koushinryou/Ookami to Koushinryou v03.cbz";

    /// <summary>
    /// Higurashi Atonement Arc shape: the first folder's ComicInfo names a LocalizedSeries, and the second folder's files
    /// parse to that name. They only merge while the first folder is read
    /// </summary>
    private async Task<(ScannerService Scanner, int LibraryId, int SeriesId, string Root)> SeriesJoinedByLocalizedName(
        IUnitOfWork unitOfWork, DataContext context, string testcase)
    {
        var comicInfo = new ComicInfo { Series = "Spice and Wolf", LocalizedSeries = "Ookami to Koushinryou" };
        var (scanner, libraryId) = await ScanOnce(unitOfWork, testcase,
        [
            "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
            "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0002.cbz",
            LocalizedFolderFile,
        ], new Dictionary<string, ComicInfo>
        {
            ["Spice and Wolf Vol. 1 Ch. 0001.cbz"] = comicInfo,
            ["Spice and Wolf Vol. 2 Ch. 0002.cbz"] = comicInfo,
        });

        var series = await context.Series.AsNoTracking().SingleAsync(s => s.LibraryId == libraryId);
        Assert.Equal("Ookami to Koushinryou", series.LocalizedName);
        Assert.Equal(3, await SeriesFileCount(context, series.Id));
        return (scanner, libraryId, series.Id, await LibraryRoot(context, libraryId));
    }

    private static Task<int> SeriesFileCount(DataContext context, int seriesId)
    {
        return context.MangaFile.CountAsync(f => f.Chapter.Volume.SeriesId == seriesId);
    }

    [Fact]
    public async Task ScanSeries_FolderJoinedByLocalizedName_KeepsItsFiles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, _, seriesId, _) = await SeriesJoinedByLocalizedName(unitOfWork, context, "Localized Join ScanSeries - Manga");

        await scanner.ScanSeries(seriesId);

        Assert.Equal(3, await SeriesFileCount(context, seriesId));
    }

    [Fact]
    public async Task ScanLibrary_OnlyLocalizedNameFolderChanged_KeepsOtherFolders()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (scanner, libraryId, seriesId, root) = await SeriesJoinedByLocalizedName(unitOfWork, context, "Localized Join Library B - Manga");

        await scannerHelper.Scaffold(root, ["Ookami to Koushinryou/Ookami to Koushinryou v04.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Ookami to Koushinryou"), DateTime.Now.AddSeconds(2));
        await scanner.ScanLibrary(libraryId);

        Assert.Equal(4, await SeriesFileCount(context, seriesId));
        Assert.Single(await context.Series.AsNoTracking().Where(s => s.LibraryId == libraryId).ToListAsync());
        var series = await context.Series.AsNoTracking().SingleAsync(s => s.Id == seriesId);
        Assert.Equal("Ookami to Koushinryou", series.LocalizedName);

        // The next scan must still merge, so the name has to survive the scan above
        await scannerHelper.Scaffold(root, ["Ookami to Koushinryou/Ookami to Koushinryou v05.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Ookami to Koushinryou"), DateTime.Now.AddSeconds(4));
        await scanner.ScanLibrary(libraryId);

        Assert.Equal(5, await SeriesFileCount(context, seriesId));
    }

    [Fact]
    public async Task ScanLibrary_OnlyNameFolderChanged_KeepsLocalizedNameFolder()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var (scanner, libraryId, seriesId, root) = await SeriesJoinedByLocalizedName(unitOfWork, context, "Localized Join Library A - Manga");

        await scannerHelper.Scaffold(root, ["Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0003.cbz"]);
        Directory.SetLastWriteTime(Path.Join(root, "Spice and Wolf", "Spice and Wolf Vol. 2"), DateTime.Now.AddSeconds(2));
        await scanner.ScanLibrary(libraryId);

        Assert.Equal(4, await SeriesFileCount(context, seriesId));
    }

    #region Failed Files

    private const string BrokenVolume = "Spice and Wolf Vol. 2.cbz";

    private static readonly string[] OneBrokenVolume =
    [
        "Spice and Wolf/Spice and Wolf Vol. 1.cbz",
        $"Spice and Wolf/{BrokenVolume}",
    ];

    [Fact]
    public async Task FailedFile_RowHasItsStampAndSeries_SecondScanReadsNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, reader, libraryId, root) = await ScanOnceWithFailing(unitOfWork, context, "Failed File - Manga",
            OneBrokenVolume, [BrokenVolume]);

        var brokenPath = Parser.NormalizePath(Path.Join(root, "Spice and Wolf", BrokenVolume));
        var row = await context.MediaError.AsNoTracking().SingleAsync();
        Assert.Equal((brokenPath, (int?) libraryId, MediaErrorProducer.Scanner, MediaErrorReason.CorruptEpub),
            (row.FilePath, row.LibraryId, row.Producer, row.Reason));
        Assert.Equal(new FileInfo(brokenPath).Length, row.Bytes);
        Assert.True(FolderChangeCheck.IsSameWriteTime(row.FileLastWriteTimeUtc!.Value, File.GetLastWriteTimeUtc(brokenPath)));
        Assert.Equal(await SeriesId(context, libraryId, "Spice and Wolf"), row.SeriesId);

        await scanner.ScanLibrary(libraryId);

        Assert.Empty(reader.Parsed);
        Assert.Equal(1, await context.MediaError.CountAsync());
    }

    [Fact]
    public async Task FailedFileDeleted_RowGoes_NextScanReadsNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, reader, libraryId, root) = await ScanOnceWithFailing(unitOfWork, context, "Failed File Deleted - Manga",
            OneBrokenVolume, [BrokenVolume]);

        File.Delete(Path.Join(root, "Spice and Wolf", BrokenVolume));
        await scanner.ScanLibrary(libraryId);
        Assert.Empty(await context.MediaError.ToListAsync());

        reader.Parsed.Clear();
        await scanner.ScanLibrary(libraryId);
        Assert.Empty(reader.Parsed);
    }

    [Fact]
    public async Task FailedFileReplacedByValidFileWithOlderTime_IsImportedAndRowGoes()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var failing = new HashSet<string> { BrokenVolume };
        var (scanner, _, libraryId, root) = await ScanOnceWithFailing(unitOfWork, context, "Failed File Replaced - Manga",
            OneBrokenVolume, failing);

        var seriesFolder = Path.Join(root, "Spice and Wolf");
        var broken = Path.Join(seriesFolder, BrokenVolume);
        File.Copy(Path.Join(seriesFolder, "Spice and Wolf Vol. 1.cbz"), broken, true);
        File.SetLastWriteTimeUtc(broken, DateTime.UtcNow.AddYears(-4));
        failing.Clear();

        await scanner.ScanLibrary(libraryId);

        Assert.Equal(2, await SeriesFileCount(context, await SeriesId(context, libraryId, "Spice and Wolf")));
        Assert.Empty(await context.MediaError.ToListAsync());
    }

    [Fact]
    public async Task FailedFile_ForcedScanReadsItAgain()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, reader, libraryId, _) = await ScanOnceWithFailing(unitOfWork, context, "Failed File Forced - Manga",
            OneBrokenVolume, [BrokenVolume]);

        await scanner.ScanLibrary(libraryId, true);

        Assert.Contains(BrokenVolume, reader.Parsed);
        Assert.Equal(1, await context.MediaError.CountAsync());
    }

    [Fact]
    public async Task FolderWhereEveryFileFails_SecondScanReadsNothing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, reader, libraryId, _) = await ScanOnceWithFailing(unitOfWork, context, "Every File Fails - Manga",
            ["Murderbot/Murderbot Vol. 1.cbz", "Murderbot/Murderbot Vol. 2.cbz"], ["Murderbot Vol. 1.cbz", "Murderbot Vol. 2.cbz"]);
        Assert.Equal(2, await context.MediaError.CountAsync());

        await scanner.ScanLibrary(libraryId);

        Assert.Empty(reader.Parsed);
    }

    [Fact]
    public async Task DismissedFailedFile_StaysDismissedUntilTheFileChanges()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, _, libraryId, root) = await ScanOnceWithFailing(unitOfWork, context, "Failed File Dismissed - Manga",
            OneBrokenVolume, [BrokenVolume]);
        var row = await context.MediaError.SingleAsync();
        row.IsDismissed = true;
        await context.SaveChangesAsync();

        await scanner.ScanLibrary(libraryId, true);
        Assert.True((await context.MediaError.AsNoTracking().SingleAsync()).IsDismissed);

        await File.WriteAllBytesAsync(Path.Join(root, "Spice and Wolf", BrokenVolume), new byte[1200]);
        await scanner.ScanLibrary(libraryId);
        Assert.False((await context.MediaError.AsNoTracking().SingleAsync()).IsDismissed);
    }

    [Fact]
    public async Task UnreadableFolder_KeepsItsFailedFileRows()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (_, _, libraryId, root) = await ScanOnceWithFailing(unitOfWork, context, "Failed File Unreadable - Manga",
            TwoSeries, ["Berserk Vol. 2 Ch. 0002.cbz"]);

        await ScannerWithUnreadable(unitOfWork, Path.Join(root, "Berserk")).ScanLibrary(libraryId);

        Assert.Equal(1, await context.MediaError.CountAsync());
    }

    [Fact]
    public async Task ScanSeries_FailedFileBelongsToTheScannedSeries()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var failing = new HashSet<string>();
        var (scanner, _, libraryId, _) = await ScanOnceWithFailing(unitOfWork, context, "Failed File Scan Series - Manga",
            OneBrokenVolume, failing);
        var seriesId = await SeriesId(context, libraryId, "Spice and Wolf");

        failing.Add(BrokenVolume);
        await scanner.ScanSeries(seriesId);

        var row = await context.MediaError.AsNoTracking().SingleAsync();
        Assert.Equal(seriesId, row.SeriesId);
    }

    [Fact]
    public async Task ProducerRows_InAReadFolder_GoWhenTheFileChangedOrIsGone()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (scanner, _, libraryId, root) = await ScanOnceWithFailing(unitOfWork, context, "Producer Rows - Manga",
            OneBrokenVolume, []);
        var seriesFolder = Parser.NormalizePath(Path.Join(root, "Spice and Wolf"));

        MediaError ProducerRow(string path, long bytes, DateTime writeTime)
        {
            var row = new MediaErrorBuilder(path).WithProducer(MediaErrorProducer.ArchiveService)
                .WithReason(MediaErrorReason.CoverFailed).WithDetails("details").Build();
            row.LibraryId = libraryId;
            row.Bytes = bytes;
            row.FileLastWriteTimeUtc = writeTime;
            return row;
        }

        var unchanged = $"{seriesFolder}/Spice and Wolf Vol. 1.cbz";
        var changed = $"{seriesFolder}/{BrokenVolume}";
        var gone = $"{seriesFolder}/Spice and Wolf Vol. 3.cbz";
        var notRead = Parser.NormalizePath(Path.Join(root, "Not Read", "Other.cbz"));
        context.MediaError.AddRange(
            ProducerRow(unchanged, new FileInfo(unchanged).Length, File.GetLastWriteTimeUtc(unchanged)),
            ProducerRow(changed, new FileInfo(changed).Length + 1, File.GetLastWriteTimeUtc(changed)),
            ProducerRow(gone, 100, DateTime.UtcNow),
            ProducerRow(notRead, 100, DateTime.UtcNow));
        await context.SaveChangesAsync();

        await scanner.ScanLibrary(libraryId, true);

        var paths = await context.MediaError.AsNoTracking().Select(m => m.FilePath).ToListAsync();
        Assert.Equal([notRead, unchanged], paths.Order());
    }

    private sealed class FailNamedFiles(IReadingItemService inner, ISet<string> failing) : IReadingItemService
    {
        public ConcurrentQueue<string> Parsed { get; } = new();

        public int GetNumberOfPages(string filePath, MangaFormat format) => inner.GetNumberOfPages(filePath, format);

        public string GetCoverImage(string filePath, string fileName, MangaFormat format, EncodeFormat encodeFormat,
            CoverImageSize size = CoverImageSize.Default) =>
            inner.GetCoverImage(filePath, fileName, format, encodeFormat, size);

        public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1) =>
            inner.Extract(fileFilePath, targetDirectory, format, imageCount);

        public ParseFileResult ParseFile(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata)
        {
            var name = Path.GetFileName(path);
            Parsed.Enqueue(name);
            return failing.Contains(name)
                ? ParseFileResult.Failed(new ParseIssue(MediaErrorReason.CorruptEpub, "broken", "details"))
                : inner.ParseFile(path, rootPath, libraryRoot, type, enableMetadata);
        }
    }

    /// <param name="failing">File names the scan reader fails on, read on every parse so a test can change it between scans</param>
    private async Task<(ScannerService Scanner, FailNamedFiles Reader, int LibraryId, string Root)> ScanOnceWithFailing(
        IUnitOfWork unitOfWork, DataContext context, string testcase, string[] files, HashSet<string> failing)
    {
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var library = await scannerHelper.GenerateScannerData(testcase, [.. files], new Dictionary<string, ComicInfo>());
        ScannerHelper.Backdate(library.Folders.First().Path, TimeSpan.FromHours(1));

        FailNamedFiles? reader = null;
        var scanner = scannerHelper.CreateServices(wrapScanReader: inner => reader = new FailNamedFiles(inner, failing));
        await scanner.ScanLibrary(library.Id);
        reader!.Parsed.Clear();

        return (scanner, reader, library.Id, await LibraryRoot(context, library.Id));
    }

    #endregion

    private async Task<(ScannerService Scanner, int LibraryId)> ScanOnce(IUnitOfWork unitOfWork,
        string testcase, string[] files, Dictionary<string, ComicInfo>? comicInfos = null)
    {
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var library = await scannerHelper.GenerateScannerData(testcase, [.. files], comicInfos ?? new Dictionary<string, ComicInfo>());
        ScannerHelper.Backdate(library.Folders.First().Path, TimeSpan.FromHours(1));

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        return (scanner, library.Id);
    }

    private static Task<Dictionary<int, DateTime>> LastFolderScanned(DataContext context, int libraryId)
    {
        return context.Series
            .AsNoTracking()
            .Where(s => s.LibraryId == libraryId)
            .ToDictionaryAsync(s => s.Id, s => s.LastFolderScanned);
    }

    private static async Task<string> LibraryRoot(DataContext context, int libraryId)
    {
        var path = await context.FolderPath
            .Where(f => f.LibraryId == libraryId)
            .Select(f => f.Path)
            .SingleAsync();

        return Parser.NormalizePath(path);
    }
}
