using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

/// <summary>
/// Table driven coverage for what a single incremental library scan must do after files are added, removed,
/// or renamed on disk. Every case asserts the same three invariants, so a new scenario is one row.
/// </summary>
public class ScannerServiceIncrementalScanTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private const string SeriesName = "Spice and Wolf";

    /// <summary>
    /// One incremental scan scenario. Mutations apply in the order Removed, Renamed, Added, so a case can delete
    /// a path a rename would collide with, and add a path a rename just vacated.
    /// </summary>
    public sealed record ScanMutationCase
    {
        public required string Name { get; init; }
        public required string[] Initial { get; init; }

        /// <summary>
        /// Paths to delete. A directory is removed recursively.
        /// </summary>
        public string[] Removed { get; init; } = [];

        public (string From, string To)[] Renamed { get; init; } = [];
        public string[] Added { get; init; } = [];

        /// <summary>
        /// Every file the series should own after the rescan, relative to the library root.
        /// </summary>
        public required string[] ExpectedFiles { get; init; }

        /// <summary>
        /// Expected volume name to chapter count. Volume names are what GetNumberTitle produces, so "1" for Vol. 1.
        /// </summary>
        public required (string Volume, int Chapters)[] ExpectedVolumes { get; init; }

        public override string ToString() => Name;
    }

    public static TheoryData<ScanMutationCase> Cases =>
    [
        // A brand new volume folder appears. The old folder is untouched on disk, so the scanner skips it and
        // must not treat its files as missing.
        new ScanMutationCase
        {
            Name = "AddSiblingVolumeFolder",
            Initial =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
            ],
            Added = ["Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0003.cbz"],
            ExpectedFiles =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0003.cbz",
            ],
            ExpectedVolumes = [("1", 2), ("2", 1)],
        },

        // Same shape, but the new file re-uses a chapter number that a skipped folder already owns. Pins the
        // unchanged-folder guard on the matchingChapter lookup in FindOrCreateChapter.
        new ScanMutationCase
        {
            Name = "AddSiblingVolumeWithChapterNumberCollision",
            Initial =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
            ],
            Added = ["Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0001.cbz"],
            ExpectedFiles =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0001.cbz",
            ],
            ExpectedVolumes = [("1", 2), ("2", 1)],
        },

        // Volume 1 holds exactly one chapter and its folder is skipped. Pins the volumeFullyRead guard on the
        // single-volume shortcut in FindOrCreateChapter.
        new ScanMutationCase
        {
            Name = "AddFileInNewFolderOntoSkippedSingleChapterVolume",
            Initial = ["Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz"],
            Added = ["Spice and Wolf/Scans B/Spice and Wolf Vol. 1 Ch. 0002.cbz"],
            ExpectedFiles =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Scans B/Spice and Wolf Vol. 1 Ch. 0002.cbz",
            ],
            ExpectedVolumes = [("1", 2)],
        },

        new ScanMutationCase
        {
            Name = "RemoveOneFileFromVolume",
            Initial =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
            ],
            Removed = ["Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz"],
            ExpectedFiles = ["Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz"],
            ExpectedVolumes = [("1", 1)],
        },

        // Deleting a whole volume folder drops that volume while the untouched sibling folder is skipped.
        new ScanMutationCase
        {
            Name = "RemoveWholeVolumeFolder",
            Initial =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0003.cbz",
            ],
            Removed = ["Spice and Wolf/Spice and Wolf Vol. 2"],
            ExpectedFiles = ["Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz"],
            ExpectedVolumes = [("1", 1)],
        },

        new ScanMutationCase
        {
            Name = "RenameFileInPlaceKeepingVolumeAndChapter",
            Initial =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
            ],
            Renamed =
            [
                ("Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
                    "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002 [Scanlator].cbz"),
            ],
            ExpectedFiles =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002 [Scanlator].cbz",
            ],
            ExpectedVolumes = [("1", 2)],
        },

        // A rename that changes the volume marker moves the file to a new volume and leaves its former siblings
        // in Vol. 1 alone.
        new ScanMutationCase
        {
            Name = "RenameChangesVolumeMarkerAndCollidesWithSkippedChapter",
            Initial =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
                "Spice and Wolf/Extras/Spice and Wolf Vol. 3 Ch. 0009.cbz",
            ],
            Renamed =
            [
                ("Spice and Wolf/Extras/Spice and Wolf Vol. 3 Ch. 0009.cbz",
                    "Spice and Wolf/Extras/Spice and Wolf Vol. 4 Ch. 0002.cbz"),
            ],
            ExpectedFiles =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
                "Spice and Wolf/Extras/Spice and Wolf Vol. 4 Ch. 0002.cbz",
            ],
            ExpectedVolumes = [("1", 2), ("4", 1)],
        },

        // A rename that moves a file onto an existing volume joins that volume as a new chapter rather than
        // folding into the chapter already there, and empties out its old volume.
        new ScanMutationCase
        {
            Name = "RenameMovesFileOntoSkippedSingleChapterVolume",
            Initial =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0005.cbz",
            ],
            Renamed =
            [
                ("Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0005.cbz",
                    "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 1 Ch. 0006.cbz"),
            ],
            ExpectedFiles =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 1 Ch. 0006.cbz",
            ],
            ExpectedVolumes = [("1", 2)],
        },

        new ScanMutationCase
        {
            Name = "AddAndRemoveInOneScan",
            Initial =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 2/Spice and Wolf Vol. 2 Ch. 0003.cbz",
            ],
            Removed = ["Spice and Wolf/Spice and Wolf Vol. 2"],
            Added = ["Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0003.cbz"],
            ExpectedFiles =
            [
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0001.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0002.cbz",
                "Spice and Wolf/Spice and Wolf Vol. 1/Spice and Wolf Vol. 1 Ch. 0003.cbz",
            ],
            ExpectedVolumes = [("1", 3)],
        },
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task IncrementalScan_AppliesDiskChanges(ScanMutationCase scenario)
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);

        // The scaffolder wipes and re-creates a directory named after the testcase, so each row needs its own
        var library = await scannerHelper.GenerateScannerData($"Incremental {scenario.Name} - Manga",
            [.. scenario.Initial], new Dictionary<string, ComicInfo>());
        var root = library.Folders.First().Path;

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        var seriesId = await GetSeriesId(unitOfWork, library.Id);
        var before = await SnapshotPlacement(context, root, seriesId);
        Assert.Equal([.. scenario.Initial.Order()], before.Keys.Order());

        await ApplyMutations(scannerHelper, root, scenario);

        await scanner.ScanLibrary(library.Id);
        await unitOfWork.CommitAsync();

        var after = await SnapshotPlacement(context, root, seriesId);

        Assert.Equal([.. scenario.ExpectedFiles.Order()], after.Keys.Order());

        var volumes = await context.Volume
            .Where(v => v.SeriesId == seriesId)
            .Select(v => new { v.Name, Chapters = v.Chapters.Count })
            .ToListAsync();
        Assert.Equal(
            [.. scenario.ExpectedVolumes.Select(v => $"{v.Volume}={v.Chapters}").Order()],
            [.. volumes.Select(v => $"{v.Name}={v.Chapters}").Order()]);

        // Any file the scenario did not touch must still sit in the exact volume and chapter it did before.
        // Chapter id alone is not enough: a reparented chapter keeps its id and changes volume.
        foreach (var untouched in Untouched(scenario))
        {
            Assert.True(after.ContainsKey(untouched), $"{untouched} was dropped from the series");
            Assert.Equal(before[untouched], after[untouched]);
        }
    }

    private static IEnumerable<string> Untouched(ScanMutationCase scenario)
    {
        var renameSources = scenario.Renamed.Select(r => r.From).ToHashSet();

        return scenario.Initial
            .Where(path => !renameSources.Contains(path))
            .Where(path => !scenario.Removed.Any(removed =>
                path == removed || path.StartsWith(removed + '/', StringComparison.Ordinal)));
    }

    private static async Task ApplyMutations(ScannerHelper scannerHelper, string root, ScanMutationCase scenario)
    {
        var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var removed in scenario.Removed)
        {
            var target = Path.Combine(root, removed);
            touched.Add(Path.GetDirectoryName(target)!);

            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }
            else
            {
                File.Delete(target);
            }
        }

        foreach (var (from, to) in scenario.Renamed)
        {
            var source = Path.Combine(root, from);
            var destination = Path.Combine(root, to);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(source, destination);

            touched.Add(Path.GetDirectoryName(source)!);
            touched.Add(Path.GetDirectoryName(destination)!);
        }

        if (scenario.Added.Length > 0)
        {
            await scannerHelper.Scaffold(root, [.. scenario.Added]);
            foreach (var added in scenario.Added)
            {
                touched.Add(Path.GetDirectoryName(Path.Combine(root, added))!);
            }
        }

        // The scanner compares folder write time against Series.LastScanned truncated to the second, so a mutation
        // made in the same second as the previous scan reads as unchanged. Push the touched folders past that.
        var changedAt = DateTime.Now.AddSeconds(2);
        foreach (var directory in touched.Where(Directory.Exists))
        {
            Directory.SetLastWriteTime(directory, changedAt);
        }
    }

    private static async Task<int> GetSeriesId(IUnitOfWork unitOfWork, int libraryId)
    {
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        return postLib.Series.Single(s => s.Name == SeriesName).Id;
    }

    /// <summary>
    /// Library relative file path to the (volume, chapter) it belongs to.
    /// </summary>
    private static async Task<Dictionary<string, (int VolumeId, int ChapterId)>> SnapshotPlacement(
        DataContext context, string root, int seriesId)
    {
        var rows = await context.MangaFile
            .Where(f => f.Chapter.Volume.SeriesId == seriesId)
            .Select(f => new { f.FilePath, f.ChapterId, f.Chapter.VolumeId })
            .AsNoTracking()
            .ToListAsync();

        var prefix = Parser.NormalizePath(root) + '/';

        return rows.ToDictionary(
            r =>
            {
                var path = Parser.NormalizePath(r.FilePath);
                Assert.StartsWith(prefix, path);
                return path[prefix.Length..];
            },
            r => (r.VolumeId, r.ChapterId));
    }
}
