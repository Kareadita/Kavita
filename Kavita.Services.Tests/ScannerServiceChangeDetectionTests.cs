using Kavita.API.Database;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Entities;
using Kavita.Models.Metadata;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
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

    [Fact(Skip = "Fails until S4: volume subfolders are not folder map keys, so each one is fully scanned")]
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

    [Fact(Skip = "Fails until S5: a Specials folder beside volume folders is never read")]
    public async Task SpecialsBesideVolumeFolders_AreAdded()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (_, libraryId) = await ScanOnce(unitOfWork, "Specials Beside Volumes - Manga",
        [
            "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
            "Spice and Wolf/Specials/Spice and Wolf SP01.cbz",
        ]);

        var files = await context.MangaFile
            .Where(f => f.Chapter.Volume.Series.LibraryId == libraryId)
            .Select(f => f.FilePath)
            .ToListAsync();

        Assert.Contains(files, f => f.EndsWith("Spice and Wolf SP01.cbz"));
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

    private async Task<(ScannerService Scanner, int LibraryId)> ScanOnce(IUnitOfWork unitOfWork,
        string testcase, string[] files)
    {
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var library = await scannerHelper.GenerateScannerData(testcase, [.. files], new Dictionary<string, ComicInfo>());
        Backdate(library.Folders.First().Path);

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        return (scanner, library.Id);
    }

    /// <summary>
    /// Pushes every write time well before the first scan, so a second scan in the same second still reads as unchanged
    /// </summary>
    private static void Backdate(string root)
    {
        var past = DateTime.Now.AddHours(-1);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetLastWriteTime(file, past);
        }

        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            Directory.SetLastWriteTime(directory, past);
        }

        Directory.SetLastWriteTime(root, past);
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
