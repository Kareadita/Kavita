using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Kavita.API.Services;
using Kavita.Database.Tests;
using Kavita.Models.Scanner;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

public class FolderPollerServiceTests : AbstractFsTest
{
    private static readonly string Library = Root + "Manga";
    private static readonly DateTime Before = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime After = Before.AddMinutes(5);

    private static MockFileSystem LibraryWithOneSeries()
    {
        var fs = new MockFileSystem();
        fs.AddFile(Library + "/Series A/Series A v01.cbz", new MockFileData("v01"));
        fs.AddDirectory(Library + "/Series A/Specials");
        foreach (var folder in new[] { Library, Library + "/Series A", Library + "/Series A/Specials" })
        {
            fs.Directory.SetLastWriteTimeUtc(folder, Before);
        }
        return fs;
    }

    private static FolderPollerService.FolderList Walked(IFileSystem fs)
    {
        var list = new FolderPollerService.FolderList(Library, fs);
        list.Walk(CancellationToken.None);
        return list;
    }

    [Fact]
    public void Walk_ListsTheRootAndEveryFolderBelow()
    {
        Assert.Equal(3, Walked(LibraryWithOneSeries()).Count);
    }

    [Fact]
    public void UnchangedPass_HandsNothing()
    {
        var list = Walked(LibraryWithOneSeries());

        var result = list.Pass(CancellationToken.None);

        Assert.True(result.Reachable);
        Assert.Empty(result.Handed);
    }

    [Fact]
    public void NewFileInSeriesFolder_HandsTheSeriesFolder()
    {
        var fs = LibraryWithOneSeries();
        var list = Walked(fs);

        fs.AddFile(Library + "/Series A/Series A v02.cbz", new MockFileData("v02"));
        fs.Directory.SetLastWriteTimeUtc(Library + "/Series A", After);

        Assert.Equal([Library + "/Series A"], list.Pass(CancellationToken.None).Handed);
        Assert.Empty(list.Pass(CancellationToken.None).Handed);
    }

    [Fact]
    public void NewSeriesTree_HandsSeriesAndAddsNestedFolders()
    {
        var fs = LibraryWithOneSeries();
        var list = Walked(fs);

        fs.AddFile(Library + "/New Series/Vol 1/c001.cbz", new MockFileData("c001"));
        fs.Directory.SetLastWriteTimeUtc(Library + "/New Series", Before);
        fs.Directory.SetLastWriteTimeUtc(Library + "/New Series/Vol 1", Before);
        fs.Directory.SetLastWriteTimeUtc(Library, After);

        Assert.Equal([Library + "/New Series"], list.Pass(CancellationToken.None).Handed);
        Assert.Equal(5, list.Count);

        fs.AddFile(Library + "/New Series/Vol 1/c002.cbz", new MockFileData("c002"));
        fs.Directory.SetLastWriteTimeUtc(Library + "/New Series/Vol 1", After);

        Assert.Equal([Library + "/New Series/Vol 1"], list.Pass(CancellationToken.None).Handed);
    }

    [Fact]
    public void RemovedSeriesFolder_HandedAndDropped()
    {
        var fs = LibraryWithOneSeries();
        var list = Walked(fs);

        fs.Directory.Delete(Library + "/Series A", true);
        fs.Directory.SetLastWriteTimeUtc(Library, After);

        Assert.Equal([Library + "/Series A"], list.Pass(CancellationToken.None).Handed);
        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void RootUnreachable_KeepsListHandsNothing()
    {
        var fs = LibraryWithOneSeries();
        var list = Walked(fs);

        fs.Directory.Delete(Library, true);

        var result = list.Pass(CancellationToken.None);
        Assert.False(result.Reachable);
        Assert.Empty(result.Handed);
        Assert.Equal(3, list.Count);
    }

    [Theory]
    [InlineData(1, 60)]
    [InlineData(9, 90)]
    [InlineData(30, 300)]
    public void Wait_IsTenTimesPassWithSixtySecondFloor(int passSeconds, int waitSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(waitSeconds), FolderPollerService.WaitAfter(TimeSpan.FromSeconds(passSeconds)));
    }

    [Fact]
    public void Restart_KeepsListForFolderStillPolled()
    {
        var fs = LibraryWithOneSeries();
        fs.AddDirectory(Root + "Comics");
        var poller = new FolderPollerService(fs, Substitute.For<ILogger<FolderPollerService>>());
        var mount = new MountInfo("/", "//nas/Media", "cifs");

        try
        {
            poller.Poll([new PolledFolder(Library, mount)]);
            var before = poller.ListFor(Library);

            poller.Poll([new PolledFolder(Library, mount), new PolledFolder(Root + "Comics", mount)]);

            Assert.Same(before, poller.ListFor(Library));
            Assert.NotNull(poller.ListFor(Root + "Comics"));

            poller.Poll([new PolledFolder(Root + "Comics", mount)]);
            Assert.Null(poller.ListFor(Library));
        }
        finally
        {
            poller.Stop();
        }
    }
}
