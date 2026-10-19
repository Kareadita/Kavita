using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Scanner;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class FolderWatchServiceTests : WatchedLibraryFoldersTest
{
    private readonly HashSet<string> _networkFolders = [];
    private readonly IFolderPollerService _folderPoller = Substitute.For<IFolderPollerService>();
    private readonly NativeFileWatcherService _nativeFileWatcher;

    public FolderWatchServiceTests()
    {
        _nativeFileWatcher = CreateNativeWatcher();
    }

    /// <summary>
    /// The library lookup yields before returning, so two starts can interleave the way two Hangfire jobs do
    /// </summary>
    private FolderWatchService CreateFolderWatchService()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SettingsRepository.GetSettingsDtoAsync()
            .Returns(new ServerSettingDto { EnableFolderWatching = true });
        unitOfWork.LibraryRepository.GetLibrariesAsync(Arg.Any<LibraryIncludes>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await Task.Delay(50);
            var library = new LibraryBuilder("Manga").Build();
            library.FolderWatchMode = FolderWatchMode.Automatic;
            library.Folders = LibraryFolders.Select(f => new FolderPath { Path = f }).ToList();
            return (IEnumerable<Library>) [library];
        });

        var mountDetector = Substitute.For<IMountDetectorService>();
        mountDetector.Detect(Arg.Any<string>()).Returns(c => _networkFolders.Contains(Parser.NormalizePath(c.Arg<string>()))
            ? new MountInfo("/nas", "//nas/Media", "cifs")
            : new MountInfo("/", "/dev/sda1", "ext4"));

        return new FolderWatchService(unitOfWork, Substitute.For<ILogger<FolderWatchService>>(), Substitute.For<ITaskScheduler>(),
            mountDetector, _nativeFileWatcher, _folderPoller);
    }

    [Fact]
    public async Task TwoRestartsAtOnce_LeaveOneWatcherPerFolder()
    {
        await Task.WhenAll(CreateFolderWatchService().RestartWatching(), CreateFolderWatchService().RestartWatching());

        Assert.Equal(5, _nativeFileWatcher.Watchers.Count);
    }

    [Fact]
    public async Task Start_WithUnreachableFolder_WatchesTheRestAndSchedulesOneRetry()
    {
        Unreachable.Add(LibraryFolders[0]);
        var folderWatchService = CreateFolderWatchService();

        var firstRetry = await folderWatchService.StartWatchersAsync();
        var secondRetry = await folderWatchService.StartWatchersAsync();

        Assert.Equal(4, _nativeFileWatcher.Watchers.Count);
        Assert.Equal(TimeSpan.FromMinutes(5), firstRetry);
        Assert.Null(secondRetry);
    }

    [Fact]
    public async Task Start_FolderGoneAfterExistsCheck_WatchesTheRestAndSchedulesARetry()
    {
        var gone = Parser.NormalizePath(Path.Join(TestRoot, "Gone"));
        GoneAfterExistsCheck.Add(gone);
        LibraryFolders.Insert(0, gone);

        var retry = await CreateFolderWatchService().StartWatchersAsync();

        Assert.Equal(5, _nativeFileWatcher.Watchers.Count);
        Assert.Equal(TimeSpan.FromMinutes(5), retry);
    }

    [Fact]
    public async Task Start_NetworkMount_PollsInsteadOfWatching()
    {
        _networkFolders.Add(LibraryFolders[0]);

        await CreateFolderWatchService().StartWatchersAsync();

        Assert.Empty(_nativeFileWatcher.Watchers);
        _folderPoller.Received().Poll(Arg.Is<IReadOnlyList<PolledFolder>>(folders => folders.Count == 5));
    }
}
