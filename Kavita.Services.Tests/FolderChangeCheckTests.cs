using Kavita.Models.Parser;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests;

public class FolderChangeCheckTests
{
    private const string Folder = "M:/March Story";
    private const string V01 = Folder + "/March Story v01.cbz";
    private const string V02 = Folder + "/March Story v02.cbz";

    private static readonly DateTime Written = new(2022, 4, 28, 20, 12, 21, DateTimeKind.Utc);
    private static readonly DateTime LastScanned = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();

    private static SeriesModified Owner(DateTime lastScanned, params KnownFile[] files)
    {
        return new SeriesModified
        {
            SeriesName = "March Story",
            FolderPath = Folder,
            LowestFolderPath = Folder,
            LastScanned = lastScanned,
            FilesByFolder = files
                .GroupBy(f => f.Path[..f.Path.LastIndexOf('/')])
                .ToDictionary(g => g.Key, IReadOnlyList<KnownFile> (g) => g.ToList()),
        };
    }

    private static SeriesModified StoredOwner(DateTime? v01Time = null, DateTime? v02Time = null)
    {
        return Owner(LastScanned,
            new KnownFile(1, V01, 1000, v01Time ?? Written),
            new KnownFile(2, V02, 1000, v02Time ?? Written));
    }

    private static bool IsUnchanged(IList<FileStamp> onDisk, SeriesModified owner, Dictionary<int, DateTime>? backfill = null)
    {
        return FolderChangeCheck.IsUnchanged(onDisk, [owner], [], f => f == Folder, backfill ?? new Dictionary<int, DateTime>());
    }

    private static bool IsUnchanged(IList<FileStamp> onDisk, IList<SeriesModified> owners, params FailedFile[] failed)
    {
        return FolderChangeCheck.IsUnchanged(onDisk, owners, failed, f => f == Folder, new Dictionary<int, DateTime>());
    }

    [Fact]
    public void SameNamesSizesAndTimes_Unchanged()
    {
        Assert.True(IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(V02, 1000, Written)], StoredOwner()));
    }

    [Fact]
    public void SubSecondDifference_Unchanged()
    {
        Assert.True(IsUnchanged([new FileStamp(V01, 1000, Written.AddMilliseconds(400)), new FileStamp(V02, 1000, Written)],
            StoredOwner()));
    }

    [Fact]
    public void CopyOverWithOlderTimeAndOtherSize_Changed()
    {
        var copiedOver = new DateTime(2020, 12, 8, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(IsUnchanged([new FileStamp(V01, 900, copiedOver), new FileStamp(V02, 1000, Written)], StoredOwner()));
    }

    [Fact]
    public void SameSizeOtherTime_Changed()
    {
        Assert.False(IsUnchanged([new FileStamp(V01, 1000, Written.AddDays(1)), new FileStamp(V02, 1000, Written)], StoredOwner()));
    }

    [Fact]
    public void OtherSizeSameTime_Changed()
    {
        Assert.False(IsUnchanged([new FileStamp(V01, 1001, Written), new FileStamp(V02, 1000, Written)], StoredOwner()));
    }

    [Fact]
    public void NewFileWithOldTime_Changed()
    {
        var rsynced = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(IsUnchanged(
        [
            new FileStamp(V01, 1000, Written),
            new FileStamp(V02, 1000, Written),
            new FileStamp(Folder + "/March Story v03.cbz", 1000, rsynced),
        ], StoredOwner()));
    }

    [Fact]
    public void MissingFile_Changed()
    {
        Assert.False(IsUnchanged([new FileStamp(V01, 1000, Written)], StoredOwner()));
    }

    [Fact]
    public void RenamedFile_Changed()
    {
        Assert.False(IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(Folder + "/March Story v2.cbz", 1000, Written)],
            StoredOwner()));
    }

    [Fact]
    public void WindowsSeparatorsOnDisk_AreNormalized()
    {
        Assert.True(IsUnchanged(
        [
            new FileStamp(@"M:\March Story\March Story v01.cbz", 1000, Written),
            new FileStamp(@"M:\March Story\March Story v02.cbz", 1000, Written),
        ], StoredOwner()));
    }

    [Fact]
    public void StoredTimeInSameSecondAsLastScan_Changed()
    {
        var sameSecond = LastScanned.ToUniversalTime().AddMilliseconds(1);
        Assert.False(IsUnchanged([new FileStamp(V01, 1000, sameSecond), new FileStamp(V02, 1000, Written)],
            StoredOwner(v01Time: sameSecond)));
    }

    [Fact]
    public void NoStoredTime_OlderThanLastScan_UnchangedAndBackfilled()
    {
        var owner = Owner(LastScanned, new KnownFile(1, V01, 1000, null), new KnownFile(2, V02, 1000, Written));
        var backfill = new Dictionary<int, DateTime>();

        Assert.True(IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(V02, 1000, Written)], owner, backfill));
        Assert.Equal(new Dictionary<int, DateTime> { [1] = Written }, backfill);
    }

    [Fact]
    public void NoStoredTime_WrittenAfterLastScan_ChangedAndNotBackfilled()
    {
        var owner = Owner(LastScanned, new KnownFile(1, V01, 1000, null), new KnownFile(2, V02, 1000, Written));
        var backfill = new Dictionary<int, DateTime>();

        Assert.False(IsUnchanged([new FileStamp(V01, 1000, LastScanned.ToUniversalTime().AddMinutes(1)), new FileStamp(V02, 1000, Written)],
            owner, backfill));
        Assert.Empty(backfill);
    }

    [Fact]
    public void NoStoredTime_OtherSize_ChangedAndNotBackfilled()
    {
        var owner = Owner(LastScanned, new KnownFile(1, V01, 1000, null), new KnownFile(2, V02, 1000, Written));
        var backfill = new Dictionary<int, DateTime>();

        Assert.False(IsUnchanged([new FileStamp(V01, 900, Written), new FileStamp(V02, 1000, Written)], owner, backfill));
        Assert.Empty(backfill);
    }

    [Fact]
    public void ChangedFolder_DoesNotBackfillMatchingFiles()
    {
        var owner = Owner(LastScanned, new KnownFile(1, V01, 1000, null), new KnownFile(2, V02, 1000, Written));
        var backfill = new Dictionary<int, DateTime>();

        Assert.False(IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(V02, 1000, Written.AddDays(1))], owner, backfill));
        Assert.Empty(backfill);
    }

    [Fact]
    public void FilesOutsideScope_AreIgnored()
    {
        var owner = Owner(LastScanned,
            new KnownFile(1, V01, 1000, Written),
            new KnownFile(2, "M:/March Story/Extras/March Story SP01.cbz", 1000, Written));

        Assert.True(IsUnchanged([new FileStamp(V01, 1000, Written)], owner));
    }

    [Fact]
    public void SharedFolder_FilesOfEveryOwnerAreKnown()
    {
        var arc1 = Owner(LastScanned, new KnownFile(1, V01, 1000, Written));
        var arc2 = Owner(LastScanned, new KnownFile(2, V02, 1000, Written));

        Assert.True(FolderChangeCheck.IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(V02, 1000, Written)],
            [arc1, arc2], [], f => f == Folder, new Dictionary<int, DateTime>()));
        Assert.False(FolderChangeCheck.IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(V02, 1000, Written)],
            [arc1], [], f => f == Folder, new Dictionary<int, DateTime>()));
    }

    [Fact]
    public void EmptyFolder_WithNoKnownFiles_Unchanged()
    {
        Assert.True(IsUnchanged([], Owner(LastScanned)));
    }

    private const string Broken = Folder + "/March Story v03.cbz";

    [Fact]
    public void FailedFile_SameSizeAndTime_IsKnown()
    {
        Assert.True(IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(Broken, 500, Written)],
            [Owner(LastScanned, new KnownFile(1, V01, 1000, Written))], new FailedFile(Broken, 500, Written)));
    }

    [Fact]
    public void FailedFile_OtherSize_Changed()
    {
        Assert.False(IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(Broken, 501, Written)],
            [Owner(LastScanned, new KnownFile(1, V01, 1000, Written))], new FailedFile(Broken, 500, Written)));
    }

    [Fact]
    public void FailedFile_OtherTime_Changed()
    {
        Assert.False(IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(Broken, 500, Written.AddDays(1))],
            [Owner(LastScanned, new KnownFile(1, V01, 1000, Written))], new FailedFile(Broken, 500, Written)));
    }

    [Fact]
    public void FailedFile_Deleted_Changed()
    {
        Assert.False(IsUnchanged([new FileStamp(V01, 1000, Written)],
            [Owner(LastScanned, new KnownFile(1, V01, 1000, Written))], new FailedFile(Broken, 500, Written)));
    }

    [Fact]
    public void FailedFile_OutsideScope_IsIgnored()
    {
        Assert.True(IsUnchanged([new FileStamp(V01, 1000, Written)],
            [Owner(LastScanned, new KnownFile(1, V01, 1000, Written))],
            new FailedFile("M:/March Story/Extras/March Story SP01.cbz", 500, Written)));
    }

    [Fact]
    public void OnlyFailedFiles_NoOwner_Unchanged()
    {
        Assert.True(IsUnchanged([new FileStamp(Broken, 500, Written)], [], new FailedFile(Broken, 500, Written)));
    }

    [Fact]
    public void StaleFailedRowForStoredFile_StoredFileWins()
    {
        Assert.True(IsUnchanged([new FileStamp(V01, 1000, Written), new FileStamp(V02, 1000, Written)],
            [StoredOwner()], new FailedFile(V02, 500, Written.AddDays(-30))));
    }
}
