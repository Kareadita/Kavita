using System.IO.Abstractions;
using Hangfire;
using Hangfire.InMemory;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// Five real library folders on disk, for tests that run real <see cref="System.IO.FileSystemWatcher"/>s
/// </summary>
public abstract class WatchedLibraryFoldersTest : IDisposable
{
    protected readonly string TestRoot = Path.Join(Path.GetTempPath(), "WatchedLibraryFoldersTest", Guid.NewGuid().ToString());
    protected readonly List<string> LibraryFolders = [];
    /// <summary>Exists reports these as gone, though they are on disk</summary>
    protected readonly HashSet<string> Unreachable = [];
    /// <summary>Exists reports these as there, though they are not on disk</summary>
    protected readonly HashSet<string> GoneAfterExistsCheck = [];
    private readonly List<NativeFileWatcherService> _watchers = [];

    protected WatchedLibraryFoldersTest()
    {
        JobStorage.Current = new InMemoryStorage();
        for (var i = 0; i < 5; i++)
        {
            var folder = Path.Join(TestRoot, $"Library{i}");
            Directory.CreateDirectory(folder);
            LibraryFolders.Add(Parser.NormalizePath(folder));
        }
    }

    protected NativeFileWatcherService CreateNativeWatcher(TimeProvider? timeProvider = null)
    {
        var directory = Substitute.For<IDirectory>();
        directory.Exists(Arg.Any<string?>()).Returns(c =>
        {
            var path = Parser.NormalizePath(c.Arg<string?>()!);
            return (Directory.Exists(path) || GoneAfterExistsCheck.Contains(path)) && !Unreachable.Contains(path);
        });
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.Directory.Returns(directory);

        var watcher = new NativeFileWatcherService(fileSystem, Substitute.For<ILogger<NativeFileWatcherService>>(),
            timeProvider ?? TimeProvider.System);
        _watchers.Add(watcher);
        return watcher;
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Stop();
        }

        if (Directory.Exists(TestRoot))
        {
            Directory.Delete(TestRoot, true);
        }
        GC.SuppressFinalize(this);
    }
}
