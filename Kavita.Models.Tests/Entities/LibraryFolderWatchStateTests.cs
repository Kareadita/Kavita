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
    [InlineData(FolderWatchMode.Automatic, FolderWatchMethod.FileWatcher)]
    [InlineData(FolderWatchMode.Polling, FolderWatchMethod.Polling)]
    public void Method_OnlyTrialMounts(FolderWatchMode mode, FolderWatchMethod expected)
    {
        Assert.Equal(expected, State(LibraryFolderWatchState.MountResult.Trial).Method(mode));
    }
}
