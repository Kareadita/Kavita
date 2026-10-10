using Hangfire;
using Hangfire.InMemory;
using Kavita.Models.DTOs;
using Kavita.Models.Scanner;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class TaskSchedulerFolderScanTests
{
    private const int MangaId = 1;
    private const int BooksId = 2;
    private const int FictionId = 3;

    private static readonly LibraryDto[] Libraries =
    [
        new() { Id = MangaId, Folders = ["M:/"] },
        new() { Id = BooksId, Folders = ["B:/"] },
        new() { Id = FictionId, Folders = ["B:/Fiction"] },
    ];

    public TaskSchedulerFolderScanTests()
    {
        JobStorage.Current = new InMemoryStorage();
    }

    [Fact]
    public void GetScheduledFolderScans_GroupsByLibraryAndTrigger()
    {
        Schedule(new ScanFolderRequest("M:/Accel World", "M:/Accel World/v02.cbz", false), 30);
        Schedule(new ScanFolderRequest("M:/Accel World", "M:/Accel World/v03.cbz", false), 60);
        Schedule(new ScanFolderRequest("M:/Berserk", "M:/Berserk/v02.cbz", false), 40);
        BackgroundJob.Enqueue<ScannerService>(s => s.ScanFolder(new ScanFolderRequest("M:/Berserk", string.Empty, false)));
        Schedule(new ScanFolderRequest("B:/Fiction/Other", "B:/Fiction/Other/a.epub", false), 120);
        Schedule(new ScanFolderRequest("X:/Gone", "X:/Gone/a.cbz", false), 150);

        var scans = TaskScheduler.GetScheduledFolderScans(Libraries);

        Assert.Equal(
        [
            (MangaId, false, "M:/Berserk", 1),
            (MangaId, true, "M:/Accel World|M:/Berserk", 2),
            (FictionId, true, "B:/Fiction/Other", 1),
            ((int?) null, true, "X:/Gone", 1),
        ], scans.Select(s => (s.LibraryId, s.FromFolderWatcher, string.Join('|', s.Folders), s.FolderCount)));
        Assert.Null(scans[0].RunAtUtc);
        Assert.All(scans.Skip(1), s => Assert.NotNull(s.RunAtUtc));
    }

    [Fact]
    public void GetScheduledFolderScans_ListsTheSoonestFolders()
    {
        for (var i = 0; i < 7; i++)
        {
            Schedule(new ScanFolderRequest($"M:/Series {i}", $"M:/Series {i}/v01.cbz", false), 30 + i);
        }

        var scan = Assert.Single(TaskScheduler.GetScheduledFolderScans(Libraries));

        Assert.Equal(["M:/Series 0", "M:/Series 1", "M:/Series 2", "M:/Series 3", "M:/Series 4"], scan.Folders);
        Assert.Equal(7, scan.FolderCount);
    }

    private static void Schedule(ScanFolderRequest request, int seconds)
    {
        BackgroundJob.Schedule<ScannerService>(s => s.ScanFolder(request), TimeSpan.FromSeconds(seconds));
    }
}
