using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using System.IO.Abstractions;
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

        public ParserInfo? ParseFile(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata)
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
