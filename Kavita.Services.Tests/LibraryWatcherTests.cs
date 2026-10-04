using System.IO.Abstractions.TestingHelpers;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

public class LibraryWatcherTests
{
    private static LibraryWatcher CreateWatcher()
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem());
        return new LibraryWatcher(ds, Substitute.For<IUnitOfWork>(), Substitute.For<ILogger<LibraryWatcher>>(),
            Substitute.For<IHostEnvironment>(), Substitute.For<ITaskScheduler>());
    }

    [Theory]
    [InlineData(new[] {"B:/Fiction"}, "B:/Fiction/Author/Book.epub", "B:/Fiction/Author")]
    [InlineData(new[] {"B:/Fiction"}, "B:/Fiction/Author/Series/Book.epub", "B:/Fiction/Author")]
    [InlineData(new[] {"M:/"}, "M:/Accel World/Accel World v01.cbz", "M:/Accel World")]
    [InlineData(new[] {"M:/"}, "M:/Accel World v01.cbz", "")]
    [InlineData(new[] {"B:/Fiction"}, "C:/Other/Book.epub", "")]
    public void GetFolder_ReturnsTopFolderUnderLibraryRoot(string[] libraryFolders, string filePath, string expected)
    {
        // GetParentDirectoryName goes through System.IO, which reads B:/ as a relative path off Windows
        if (!OperatingSystem.IsWindows()) return;

        Assert.Equal(expected, CreateWatcher().GetFolder(filePath, libraryFolders));
    }

    [Theory]
    [InlineData(new[] {"B:/Fiction", "B:/Fiction2"}, "B:/Fiction2/Author/Book.epub", "B:/Fiction2/Author")]
    [InlineData(new[] {"B:/", "B:/Fiction"}, "B:/Fiction/Author/Book.epub", "B:/Fiction/Author")]
    public void GetFolder_PicksTheLibraryRootTheFileIsIn(string[] libraryFolders, string filePath, string expected)
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Equal(expected, CreateWatcher().GetFolder(filePath, libraryFolders));
    }
}
