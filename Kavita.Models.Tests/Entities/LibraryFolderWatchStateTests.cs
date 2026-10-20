using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Tests.Entities;

public class LibraryFolderWatchStateTests
{
    private static LibraryFolderWatchState State(params LibraryFolderWatchState.MountResult[] results) => new()
    {
        Mounts = results.Select((r, i) => new LibraryFolderWatchState.MountEntry { Fingerprint = $"mount{i}", Result = r }).ToList(),
    };

    [Theory]
    [InlineData(FolderWatchMode.Automatic, FolderWatchMethod.Polling)]
    [InlineData(FolderWatchMode.FileWatcher, FolderWatchMethod.FileWatcher)]
    [InlineData(FolderWatchMode.Polling, FolderWatchMethod.Polling)]
    [InlineData(FolderWatchMode.Off, FolderWatchMethod.Off)]
    public void Method_LocalAndNetworkMount(FolderWatchMode mode, FolderWatchMethod expected)
    {
        var state = State(LibraryFolderWatchState.MountResult.Trial, LibraryFolderWatchState.MountResult.Network);

        Assert.Equal(expected, state.Method(mode));
    }

    [Theory]
    [InlineData(FolderWatchMode.Automatic, FolderWatchMethod.Trial)]
    [InlineData(FolderWatchMode.FileWatcher, FolderWatchMethod.FileWatcher)]
    [InlineData(FolderWatchMode.Polling, FolderWatchMethod.Polling)]
    public void Method_TrialAndWatchingMount(FolderWatchMode mode, FolderWatchMethod expected)
    {
        var state = State(LibraryFolderWatchState.MountResult.Trial, LibraryFolderWatchState.MountResult.Watching);

        Assert.Equal(expected, state.Method(mode));
    }

    [Fact]
    public void Method_OnlyWatchingMounts_IsFileWatcher()
    {
        var state = State(LibraryFolderWatchState.MountResult.Watching, LibraryFolderWatchState.MountResult.Watching);

        Assert.Equal(FolderWatchMethod.FileWatcher, state.Method(FolderWatchMode.Automatic));
    }

    [Theory]
    [InlineData(FolderWatchMode.Automatic, FolderWatchMethod.Failed)]
    [InlineData(FolderWatchMode.FileWatcher, FolderWatchMethod.FileWatcher)]
    public void Method_FailedAndNetworkMount(FolderWatchMode mode, FolderWatchMethod expected)
    {
        var state = State(LibraryFolderWatchState.MountResult.Network, LibraryFolderWatchState.MountResult.Failed);

        Assert.Equal(expected, state.Method(mode));
    }
}
