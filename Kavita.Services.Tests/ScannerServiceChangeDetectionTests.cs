using Kavita.API.Database;
using Kavita.Common.Extensions;
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
        Backdate(root);

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

    private async Task<(ScannerService Scanner, int LibraryId)> ScanOnce(IUnitOfWork unitOfWork,
        string testcase, string[] files, Dictionary<string, ComicInfo>? comicInfos = null)
    {
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);
        var library = await scannerHelper.GenerateScannerData(testcase, [.. files], comicInfos ?? new Dictionary<string, ComicInfo>());
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
