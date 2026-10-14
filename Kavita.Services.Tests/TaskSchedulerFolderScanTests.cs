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

        Schedule(new ScanFolderRequest(accelWorld, accelWorld + "/v02.cbz", false), 30);
        Schedule(new ScanFolderRequest(accelWorld, accelWorld + "/v03.cbz", false), 60);
        Schedule(new ScanFolderRequest(berserk, berserk + "/v02.cbz", false), 40);
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanFolder(new ScanFolderRequest(berserk, string.Empty, false)));
        Schedule(new ScanFolderRequest(other, other + "/a.epub", false), 120);
        Schedule(new ScanFolderRequest(gone, gone + "/a.cbz", false), 150);

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
            Schedule(new ScanFolderRequest($"{Manga}/Series {i}", $"{Manga}/Series {i}/v01.cbz", false), 30 + i);
        }

        var scan = Assert.Single(TaskScheduler.GetScheduledFolderScans(Libraries));

        Assert.Equal(Enumerable.Range(0, 5).Select(i => $"{Manga}/Series {i}"), scan.Folders);
        Assert.Equal(7, scan.FolderCount);
    }

    private static void Schedule(ScanFolderRequest request, int seconds)
    {
        BackgroundJob.Schedule<ScannerService>(s => s.ScanFolder(request), TimeSpan.FromSeconds(seconds));
    }
}
