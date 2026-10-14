using Hangfire;
using Hangfire.InMemory;
using Kavita.Database.Tests;
using Kavita.Models.DTOs;
using Kavita.Models.Scanner;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class TaskSchedulerFolderScanTests : AbstractFsTest
{
    private const int MangaId = 1;
    private const int BooksId = 2;
    private const int FictionId = 3;

    private static readonly string Manga = Root + "Manga";
    private static readonly string Books = Root + "Books";

    private static readonly LibraryDto[] Libraries =
    [
        new() { Id = MangaId, Folders = [Manga] },
        new() { Id = BooksId, Folders = [Books] },
        new() { Id = FictionId, Folders = [Books + "/Fiction"] },
    ];

    public TaskSchedulerFolderScanTests()
    {
        JobStorage.Current = new InMemoryStorage();
    }

    [Fact]
    public void GetScheduledFolderScans_GroupsByLibraryAndTrigger()
    {
        var accelWorld = Manga + "/Accel World";
        var berserk = Manga + "/Berserk";
        var other = Books + "/Fiction/Other";
        var gone = Root + "Gone";
        const int deletedLibraryId = 99;

        Schedule(Watcher(MangaId, accelWorld, accelWorld + "/v02.cbz"), 30);
        Schedule(Watcher(MangaId, accelWorld, accelWorld + "/v03.cbz"), 60);
        Schedule(Watcher(MangaId, berserk, berserk + "/v02.cbz"), 40);
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanFolder(new ScanFolderRequest(MangaId, berserk, berserk, false, false)));
        Schedule(Watcher(FictionId, other, other + "/a.epub"), 120);
        Schedule(Watcher(deletedLibraryId, gone, gone + "/a.cbz"), 150);

        var scans = TaskScheduler.GetScheduledFolderScans(Libraries);

        Assert.Equal(
        [
            (MangaId, false, berserk, 1),
            (MangaId, true, accelWorld + "|" + berserk, 2),
            (FictionId, true, other, 1),
            ((int?) null, true, gone, 1),
        ], scans.Select(s => (s.LibraryId, s.FromFolderWatcher, string.Join('|', s.Folders), s.FolderCount)));
        Assert.Null(scans[0].RunAtUtc);
        Assert.All(scans.Skip(1), s => Assert.NotNull(s.RunAtUtc));
    }

    [Fact]
    public void GetScheduledFolderScans_ListsTheSoonestFolders()
    {
        for (var i = 0; i < 7; i++)
        {
            Schedule(Watcher(MangaId, $"{Manga}/Series {i}", $"{Manga}/Series {i}/v01.cbz"), 30 + i);
        }

        var scan = Assert.Single(TaskScheduler.GetScheduledFolderScans(Libraries));

        Assert.Equal(Enumerable.Range(0, 5).Select(i => $"{Manga}/Series {i}"), scan.Folders);
        Assert.Equal(7, scan.FolderCount);
    }

    [Theory]
    [InlineData("Manga/Dr. Stone", false, MangaId, "Manga/Dr. Stone")]
    [InlineData("Manga/Dr. Stone/Dr. Stone v01.cbz", true, MangaId, "Manga/Dr. Stone")]
    [InlineData("Manga/YenPress/Frieren", false, MangaId, "Manga/YenPress")]
    [InlineData("Manga", false, MangaId, "Manga")]
    [InlineData("Manga/Accel World v01.cbz", true, MangaId, "Manga")]
    [InlineData("Books/Fiction/Author/Book.epub", true, FictionId, "Books/Fiction/Author")]
    [InlineData("Books/Author/Book.epub", true, BooksId, "Books/Author")]
    [InlineData("Books/Fiction", false, FictionId, "Books/Fiction")]
    public void ResolveScanFolder_DeepestRootAndTopFolder(string path, bool isFile, int libraryId, string folder)
    {
        var target = TaskScheduler.ResolveScanFolder(Root + path, isFile, Libraries);

        Assert.Equal((libraryId, Root + folder), (target?.LibraryId, target?.Folder));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveScanFolder_NestedRootsInEitherOrder_PicksTheDeepest(bool deepestFirst)
    {
        LibraryDto[] libraries = deepestFirst ? [Libraries[2], Libraries[1]] : [Libraries[1], Libraries[2]];

        var target = TaskScheduler.ResolveScanFolder(Books + "/Fiction/Author/Series", false, libraries);

        Assert.Equal((FictionId, Books + "/Fiction/Author"), (target?.LibraryId, target?.Folder));
    }

    [Theory]
    [InlineData("Other/Series")]
    [InlineData("Books2/Series")]
    public void ResolveScanFolder_PathInNoLibrary_IsNull(string path)
    {
        Assert.Null(TaskScheduler.ResolveScanFolder(Root + path, false, Libraries));
    }

    private static ScanFolderRequest Watcher(int libraryId, string folder, string changedPath) => new(libraryId, folder, changedPath, true, false);

    private static void Schedule(ScanFolderRequest request, int seconds)
    {
        BackgroundJob.Schedule<ScannerService>(s => s.ScanFolder(request), TimeSpan.FromSeconds(seconds));
    }
}
