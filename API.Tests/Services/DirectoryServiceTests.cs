using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using API.Services;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class DirectoryServiceTests
{
    private readonly ILogger<DirectoryService> _logger = Substitute.For<ILogger<DirectoryService>>();


    #region TraverseTreeParallelForEach
    [Fact]
    public void TraverseTreeParallelForEach_JustArchives_ShouldBe28()
    {
        var testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 28; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{testDirectory}file_{29}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = new List<string>();
        var fileCount = ds.TraverseTreeParallelForEach(testDirectory, s => files.Add(s),
            API.Services.Tasks.Scanner.Parser.Parser.ArchiveFileExtensions, _logger);

        Assert.Equal(28, fileCount);
        Assert.Equal(28, files.Count);
    }

    [Fact]
    public void TraverseTreeParallelForEach_LongDirectory_ShouldBe1()
    {
        var fileSystem = new MockFileSystem();
        // Create a super long path
        var testDirectory = "/manga/";
        for (var i = 0; i < 200; i++)
        {
            testDirectory = fileSystem.FileSystem.Path.Join(testDirectory, "supercalifragilisticexpialidocious");
        }


        fileSystem.AddFile(fileSystem.FileSystem.Path.Join(testDirectory, "file_29.jpg"), new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = new List<string>();
        try
        {
            var fileCount = ds.TraverseTreeParallelForEach("/manga/", s => files.Add(s),
                API.Services.Tasks.Scanner.Parser.Parser.ImageFileExtensions, _logger);
            Assert.Equal(1, fileCount);
        }
        catch
        {
            Assert.False(true);
        }


        Assert.Single(files);
    }



    [Fact]
    public void TraverseTreeParallelForEach_DontCountExcludedDirectories_ShouldBe28()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 28; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{Path.Join(testDirectory, "@eaDir")}file_{29}.jpg", new MockFileData(""));
        fileSystem.AddFile($"{Path.Join(testDirectory, ".DS_Store")}file_{30}.jpg", new MockFileData(""));
        fileSystem.AddFile($"{Path.Join(testDirectory, ".qpkg")}file_{30}.jpg", new MockFileData(""));
        fileSystem.AddFile($"{Path.Join(testDirectory, ".@_thumb")}file_{30}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = new List<string>();
        var fileCount = ds.TraverseTreeParallelForEach(testDirectory, s => files.Add(s),
            API.Services.Tasks.Scanner.Parser.Parser.ArchiveFileExtensions, _logger);

        Assert.Equal(28, fileCount);
        Assert.Equal(28, files.Count);
    }
    #endregion

    #region GetFilesWithCertainExtensions
    [Fact]
    public void GetFilesWithCertainExtensions_ShouldBe10()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{testDirectory}file_{29}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFilesWithExtension(testDirectory, API.Services.Tasks.Scanner.Parser.Parser.ArchiveFileExtensions);

        Assert.Equal(10, files.Length);
        Assert.All(files, s => fileSystem.Path.GetExtension(s).Equals(".zip"));
    }

    [Fact]
    public void GetFilesWithCertainExtensions_OnlyArchives()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{testDirectory}file_{29}.rar", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFilesWithExtension(testDirectory, ".zip|.rar");

        Assert.Equal(11, files.Length);
    }
    #endregion

    #region GetFiles
    [Fact]
    public void GetFiles_ArchiveOnly_ShouldBe10()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{testDirectory}file_{29}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFiles(testDirectory, API.Services.Tasks.Scanner.Parser.Parser.ArchiveFileExtensions).ToList();

        Assert.Equal(10, files.Count);
        Assert.All(files, s => fileSystem.Path.GetExtension(s).Equals(".zip"));
    }

    [Fact]
    public void GetFiles_All_ShouldBe11()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{testDirectory}file_{29}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFiles(testDirectory).ToList();

        Assert.Equal(11, files.Count);
    }

    [Fact]
    public void GetFiles_All_MixedPathSeparators()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"/manga\\file_{29}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFiles(testDirectory).ToList();

        Assert.Equal(11, files.Count);
    }

    [Fact]
    public void GetFiles_All_TopDirectoryOnly_ShouldBe10()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{testDirectory}/SubDir/file_{29}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFiles(testDirectory).ToList();

        Assert.Equal(10, files.Count);
    }

    [Fact]
    public void GetFiles_WithSubDirectories_ShouldCountOnlyTopLevel()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{testDirectory}/SubDir/file_{29}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFiles(testDirectory).ToList();

        Assert.Equal(10, files.Count);
    }

    [Fact]
    public void GetFiles_ShouldNotReturnFilesThatAreExcluded()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        fileSystem.AddFile($"{testDirectory}/._file_{29}.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFiles(testDirectory).ToList();

        Assert.Equal(10, files.Count);
    }

    [Fact]
    public void GetFiles_WithCustomRegex_ShouldBe10()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}data-{i}.txt", new MockFileData(""));
        }
        fileSystem.AddFile($"{testDirectory}joe.txt", new MockFileData(""));
        fileSystem.AddFile($"{testDirectory}0d.txt", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFiles(testDirectory, @".*d.*\.txt");
        Assert.Equal(11, files.Count());
    }

    [Fact]
    public void GetFiles_WithCustomRegexThatContainsFolder_ShouldBe10()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file/data-{i}.txt", new MockFileData(""));
        }
        fileSystem.AddFile($"{testDirectory}joe.txt", new MockFileData(""));
        fileSystem.AddFile($"{testDirectory}0d.txt", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var files = ds.GetFiles(testDirectory, @".*d.*\.txt", SearchOption.AllDirectories);
        Assert.Equal(11, files.Count());
    }
    #endregion

    #region GetTotalSize
    [Fact]
    public void GetTotalSize_ShouldBeGreaterThan0()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file/data-{i}.txt", new MockFileData("abc"));
        }
        fileSystem.AddFile($"{testDirectory}joe.txt", new MockFileData(""));


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var fileSize = ds.GetTotalSize(fileSystem.AllFiles);
        Assert.True(fileSize > 0);
    }
    #endregion

    #region CopyFileToDirectory
    [Fact]
    public void CopyFileToDirectory_ShouldCopyFileToNonExistentDirectory()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}file/data-0.txt", new MockFileData("abc"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFileToDirectory($"{testDirectory}file/data-0.txt", "/manga/output/");
        Assert.True(fileSystem.FileExists("manga/output/data-0.txt"));
        Assert.True(fileSystem.FileExists("manga/file/data-0.txt"));
    }
    [Fact]
    public void CopyFileToDirectory_ShouldCopyFileToExistingDirectoryAndOverwrite()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}file/data-0.txt", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}output/data-0.txt", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFileToDirectory($"{testDirectory}file/data-0.txt", "/manga/output/");
        Assert.True(fileSystem.FileExists("/manga/output/data-0.txt"));
        Assert.True(fileSystem.FileExists("/manga/file/data-0.txt"));
        Assert.True(fileSystem.FileInfo.New("/manga/file/data-0.txt").Length == fileSystem.FileInfo.New("/manga/output/data-0.txt").Length);
    }
    #endregion

    #region CopyDirectoryToDirectory
    [Fact]
    public void CopyDirectoryToDirectory_ShouldThrowWhenSourceDestinationDoesntExist()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}file/data-0.txt", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}output/data-0.txt", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var ex = Assert.Throws<DirectoryNotFoundException>(() => ds.CopyDirectoryToDirectory("/comics/", "/manga/output/"));
        Assert.Equal("Source directory does not exist or could not be found: " + "/comics/", ex.Message);
    }

    [Fact]
    public void CopyDirectoryToDirectory_ShouldCopyEmptyDirectory()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}file/data-0.txt", new MockFileData("abc"));
        fileSystem.AddDirectory($"{testDirectory}empty/");

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyDirectoryToDirectory($"{testDirectory}empty/", "/manga/output/");
        Assert.Empty(fileSystem.DirectoryInfo.New("/manga/output/").GetFiles());
    }

    [Fact]
    public void CopyDirectoryToDirectory_ShouldCopyAllFileAndNestedDirectoriesOver()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}file/data-0.txt", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}data-1.txt", new MockFileData("abc"));
        fileSystem.AddDirectory($"{testDirectory}empty/");

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyDirectoryToDirectory($"{testDirectory}", "/manga/output/");
        Assert.Equal(2, ds.GetFiles("/manga/output/", searchOption: SearchOption.AllDirectories).Count());
    }
    #endregion

    #region IsDriveMounted
    [Fact]
    public void IsDriveMounted_DriveIsNotMounted()
    {
        const string testDirectory = "c:/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}data-0.txt", new MockFileData("abc"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        Assert.False(ds.IsDriveMounted("d:/manga/"));
    }

    [Fact]
    public void IsDriveMounted_DriveIsMounted()
    {
        const string testDirectory = "c:/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}data-0.txt", new MockFileData("abc"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        Assert.True(ds.IsDriveMounted("c:/manga/file"));
    }
    #endregion

    #region IsDirectoryEmpty
    [Fact]
    public void IsDirectoryEmpty_DirectoryIsEmpty()
    {
        const string testDirectory = "c:/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(testDirectory);
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        Assert.True(ds.IsDirectoryEmpty("c:/manga/"));
    }

    [Fact]
    public void IsDirectoryEmpty_DirectoryIsNotEmpty()
    {
        const string testDirectory = "c:/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}data-0.txt", new MockFileData("abc"));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        Assert.False(ds.IsDirectoryEmpty("c:/manga/"));
    }
    #endregion

    #region ExistOrCreate
    [Fact]
    public void ExistOrCreate_ShouldCreate()
    {
        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.ExistOrCreate("c:/manga/output/");

        Assert.True(ds.FileSystem.DirectoryInfo.New("c:/manga/output/").Exists);
    }
    #endregion

    #region ClearAndDeleteDirectory
    [Fact]
    public void ClearAndDeleteDirectory_ShouldDeleteSelfAndAllFilesAndFolders()
    {
        const string testDirectory = "/manga/base/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file/data-{i}.txt", new MockFileData("abc"));
        }
        fileSystem.AddFile($"{testDirectory}data-a.txt", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}data-b.txt", new MockFileData("abc"));
        fileSystem.AddDirectory($"{testDirectory}empty/");

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.ClearAndDeleteDirectory($"{testDirectory}");
        Assert.Empty(ds.GetFiles("/manga/", searchOption: SearchOption.AllDirectories));
        Assert.Empty(ds.FileSystem.DirectoryInfo.New("/manga/").GetDirectories());
        Assert.True(ds.FileSystem.DirectoryInfo.New("/manga/").Exists);
        Assert.False(ds.FileSystem.DirectoryInfo.New("/manga/base").Exists);
    }
    #endregion

    #region ClearDirectory
    [Fact]
    public void ClearDirectory_ShouldDeleteAllFilesAndFolders_LeaveSelf()
    {
        const string testDirectory = "/manga/base/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file/data-{i}.txt", new MockFileData("abc"));
        }
        fileSystem.AddFile($"{testDirectory}data-a.txt", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}data-b.txt", new MockFileData("abc"));
        fileSystem.AddDirectory($"{testDirectory}file/empty/");

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.ClearDirectory($"{testDirectory}file/");
        Assert.Empty(ds.FileSystem.DirectoryInfo.New($"{testDirectory}file/").GetDirectories());
        Assert.True(ds.FileSystem.DirectoryInfo.New("/manga/").Exists);
        Assert.True(ds.FileSystem.DirectoryInfo.New($"{testDirectory}file/").Exists);
    }

    [Fact]
    public void ClearDirectory_ShouldDeleteFoldersWithOneFileInside()
    {
        const string testDirectory = "/manga/base/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file/data-{i}.txt", new MockFileData("abc"));
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.ClearDirectory($"{testDirectory}");
        Assert.Empty(ds.FileSystem.DirectoryInfo.New($"{testDirectory}").GetDirectories());
        Assert.True(ds.FileSystem.DirectoryInfo.New(testDirectory).Exists);
        Assert.False(ds.FileSystem.DirectoryInfo.New($"{testDirectory}file/").Exists);
    }
    #endregion

    #region CopyFilesToDirectory
    [Fact]
    public void CopyFilesToDirectory_ShouldMoveAllFiles()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFilesToDirectory(new []{$"{testDirectory}file_{0}.zip", $"{testDirectory}file_{1}.zip"}, "/manga/output/");
        Assert.Equal(2, ds.GetFiles("/manga/output/").Count());
    }

    [Fact]
    public void CopyFilesToDirectory_ShouldMoveAllFilesAndNotFailOnNonExistentFiles()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFilesToDirectory(new []{$"{testDirectory}file_{0}.zip", $"{testDirectory}file_{200}.zip", $"{testDirectory}file_{1}.zip"}, "/manga/output/");
        Assert.Equal(2, ds.GetFiles("/manga/output/").Count());
    }

    [Fact]
    public void CopyFilesToDirectory_ShouldMoveAllFiles_InclFilesInNestedFolders()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }
        fileSystem.AddFile($"{testDirectory}nested/file_11.zip", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFilesToDirectory(new []{$"{testDirectory}file_{0}.zip", $"{testDirectory}file_{1}.zip", $"{testDirectory}nested/file_11.zip"}, "/manga/output/");
        Assert.Equal(3, ds.GetFiles("/manga/output/").Count());
    }

    [Fact]
    public void CopyFilesToDirectory_ShouldMoveAllFiles_WithPrepend()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFilesToDirectory(new []{$"{testDirectory}file_{0}.zip", $"{testDirectory}file_{1}.zip", $"{testDirectory}nested/file_11.zip"},
            "/manga/output/", "mangarocks_");
        Assert.Equal(2, ds.GetFiles("/manga/output/").Count());
        Assert.All(ds.GetFiles("/manga/output/"), filepath => ds.FileSystem.Path.GetFileName(filepath).StartsWith("mangarocks_"));
    }

    [Fact]
    public void CopyFilesToDirectory_ShouldMoveOnlyFilesThatExist()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        for (var i = 0; i < 10; i++)
        {
            fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFilesToDirectory(new []{$"{testDirectory}file_{0}.zip", $"{testDirectory}file_{1}.zip", $"{testDirectory}nested/file_11.zip"},
            "/manga/output/");
        Assert.Equal(2, ds.GetFiles("/manga/output/").Count());
    }

    [Fact]
    public void CopyFilesToDirectory_ShouldAppendWhenTargetFileExists()
    {

        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(MockUnixSupport.Path($"{testDirectory}file.zip"), new MockFileData(""));
        fileSystem.AddFile(MockUnixSupport.Path($"/manga/output/file (1).zip"), new MockFileData(""));
        fileSystem.AddFile(MockUnixSupport.Path($"/manga/output/file (2).zip"), new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFilesToDirectory(new []{MockUnixSupport.Path($"{testDirectory}file.zip")}, "/manga/output/");
        ds.CopyFilesToDirectory(new []{MockUnixSupport.Path($"{testDirectory}file.zip")}, "/manga/output/");
        var outputFiles = ds.GetFiles("/manga/output/").Select(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath).ToList();
        Assert.Equal(4, outputFiles.Count); // we have 2 already there and 2 copies
        // For some reason, this has C:/ on directory even though everything is emulated (System.IO.Abstractions issue, not changing)
        // https://github.com/TestableIO/System.IO.Abstractions/issues/831
        Assert.True(outputFiles.Contains(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath("/manga/output/file (3).zip"))
                    || outputFiles.Contains(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath("C:/manga/output/file (3).zip")));
    }

    [Fact]
    public void CopyFilesToDirectory_ShouldRenameFilesToPassedNames()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(MockUnixSupport.Path($"{testDirectory}file.zip"), new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.CopyFilesToDirectory(new []{MockUnixSupport.Path($"{testDirectory}file.zip")}, "/manga/output/", new [] {"01"});
        var outputFiles = ds.GetFiles("/manga/output/").Select(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath).ToList();
        Assert.Single(outputFiles);
        // For some reason, this has C:/ on directory even though everything is emulated (System.IO.Abstractions issue, not changing)
        // https://github.com/TestableIO/System.IO.Abstractions/issues/831
        Assert.True(outputFiles.Contains(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath("/manga/output/01.zip"))
                    || outputFiles.Contains(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath("C:/manga/output/01.zip")));
    }

    #endregion

    #region ListDirectory
    [Fact]
    public void ListDirectory_EmptyForNonExistent()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}file_0.zip", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        Assert.Empty(ds.ListDirectory("/comics/"));
    }

    [Fact]
    public void ListDirectory_ListsAllDirectories()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory($"{testDirectory}dir1");
        fileSystem.AddDirectory($"{testDirectory}dir2");
        fileSystem.AddDirectory($"{testDirectory}dir3");
        fileSystem.AddFile($"{testDirectory}file_0.zip", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        Assert.Equal(3, ds.ListDirectory(testDirectory).Count());
    }

    [Fact]
    public void ListDirectory_ListsOnlyNonSystemAndHiddenOnly()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory($"{testDirectory}dir1");
        var di = fileSystem.DirectoryInfo.New($"{testDirectory}dir1");
        di.Attributes |= FileAttributes.System;
        fileSystem.AddDirectory($"{testDirectory}dir2");
        di = fileSystem.DirectoryInfo.New($"{testDirectory}dir2");
        di.Attributes |= FileAttributes.Hidden;
        fileSystem.AddDirectory($"{testDirectory}dir3");
        fileSystem.AddFile($"{testDirectory}file_0.zip", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        Assert.Single(ds.ListDirectory(testDirectory));
    }

    #endregion

    #region ReadFileAsync

    [Fact]
    public async Task ReadFileAsync_ShouldGetBytes()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}file_1.zip", new MockFileData("Hello"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var bytes = await ds.ReadFileAsync($"{testDirectory}file_1.zip");
        Assert.Equal(Encoding.UTF8.GetBytes("Hello"), bytes);
    }

    [Fact]
    public async Task ReadFileAsync_ShouldReadNothingFromNonExistent()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile($"{testDirectory}file_1.zip", new MockFileData("Hello"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var bytes = await ds.ReadFileAsync($"{testDirectory}file_32123.zip");
        Assert.Empty(bytes);
    }


    #endregion

    #region FindHighestDirectoriesFromFiles

    [Theory]
    [InlineData(new [] {"C:/Manga/"}, new [] {"C:/Manga/Love Hina/Vol. 01.cbz"}, "C:/Manga/Love Hina")]
    [InlineData(new [] {"C:/Manga/Dir 1/", "c://Manga/Dir 2/"}, new [] {"C:/Manga/Dir 1/Love Hina/Vol. 01.cbz"}, "C:/Manga/Dir 1/Love Hina")]
    [InlineData(new [] {"C:/Manga/Dir 1/", "c://Manga/"}, new [] {"D:/Manga/Love Hina/Vol. 01.cbz", "D:/Manga/Vol. 01.cbz"}, "")]
    [InlineData(new [] {"C:/Manga/"}, new [] {"C:/Manga//Love Hina/Vol. 01.cbz"}, "C:/Manga/Love Hina")]
    [InlineData(new [] {@"C:\mount\drive\Library\Test Library\Comics\"}, new [] {@"C:\mount\drive\Library\Test Library\Comics\Bruce Lee (1994)\Bruce Lee #001 (1994).cbz"}, @"C:/mount/drive/Library/Test Library/Comics/Bruce Lee (1994)")]
    public void FindHighestDirectoriesFromFilesTest(string[] rootDirectories, string[] files, string expectedDirectory)
    {
        var fileSystem = new MockFileSystem();
        foreach (var directory in rootDirectories)
        {
            fileSystem.AddDirectory(directory);
        }
        foreach (var f in files)
        {
            fileSystem.AddFile(f, new MockFileData(""));
        }
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        var actual = ds.FindHighestDirectoriesFromFiles(rootDirectories, files);
        var expected = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(expectedDirectory))
        {
            expected = new Dictionary<string, string> {{expectedDirectory, ""}};
        }

        Assert.Equal(expected, actual);
    }

    #endregion

    #region FindLowestDirectoriesFromFiles

    [Theory]
    [InlineData(new [] {"C:/Manga/"},
        new [] {"C:/Manga/Love Hina/Vol. 01.cbz"},
        "C:/Manga/Love Hina")]
    [InlineData(new [] {"C:/Manga/"},
        new [] {"C:/Manga/Romance/Love Hina/Vol. 01.cbz"},
        "C:/Manga/Romance/Love Hina")]
    [InlineData(new [] {"C:/Manga/Dir 1/", "c://Manga/Dir 2/"},
        new [] {"C:/Manga/Dir 1/Love Hina/Vol. 01.cbz"},
        "C:/Manga/Dir 1/Love Hina")]
    [InlineData(new [] {"C:/Manga/Dir 1/", "c://Manga/"},
        new [] {"D:/Manga/Love Hina/Vol. 01.cbz", "D:/Manga/Vol. 01.cbz"},
        null)]
    [InlineData(new [] {@"C:\mount\drive\Library\Test Library\Comics\"},
        new [] {@"C:\mount\drive\Library\Test Library\Comics\Bruce Lee (1994)\Bruce Lee #001 (1994).cbz"},
        @"C:/mount/drive/Library/Test Library/Comics/Bruce Lee (1994)")]
    [InlineData(new [] {"C:/Manga/"},
        new [] {"C:/Manga/Love Hina/Vol. 01.cbz", "C:/Manga/Love Hina/Specials/Sp01.cbz"},
        "C:/Manga/Love Hina")]
    [InlineData(new [] {"/manga"},
        new [] {"/manga/Love Hina/Vol. 01.cbz", "/manga/Love Hina/Specials/Sp01.cbz"},
        "/manga/Love Hina")]
    [InlineData(new [] {"/manga"},
        new [] {"/manga/Love Hina/Hina/Vol. 01.cbz", "/manga/Love Hina/Specials/Sp01.cbz"},
        "/manga/Love Hina")]
    [InlineData(new [] {"/manga"},
        new [] {"/manga/Dress Up Darling/Dress Up Darling Ch 01.cbz", "/manga/Dress Up Darling/Dress Up Darling/Dress Up Darling Vol 01.cbz"},
        "/manga/Dress Up Darling")]
    public void FindLowestDirectoriesFromFilesTest(string[] rootDirectories, string[] files, string expectedDirectory)
    {
        var fileSystem = new MockFileSystem();
        foreach (var directory in rootDirectories)
        {
            fileSystem.AddDirectory(directory);
        }
        foreach (var f in files)
        {
            fileSystem.AddFile(f, new MockFileData(""));
        }
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        var actual = ds.FindLowestDirectoriesFromFiles(rootDirectories, files);
        Assert.Equal(expectedDirectory, actual);
    }

    #endregion
    #region GetFoldersTillRoot

    [Theory]
    [InlineData("C:/Manga/", "C:/Manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
    [InlineData("C:/Manga/", "C:/Manga/Love Hina/Specials/Omake", "Omake,Specials,Love Hina")]
    [InlineData("C:/Manga", "C:/Manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
    [InlineData("C:/Manga", @"C:\Manga\Love Hina\Specials\Omake\", "Omake,Specials,Love Hina")]
    [InlineData(@"/manga/", @"/manga/Love Hina/Specials/Omake/", "Omake,Specials,Love Hina")]
    [InlineData(@"/manga/", @"/manga/", "")]
    [InlineData(@"E:\test", @"E:\test\Sweet X Trouble\Sweet X Trouble - Chapter 001.cbz", "Sweet X Trouble")]
    [InlineData(@"C:\/mount/gdrive/Library/Test Library/Comics/", @"C:\/mount/gdrive/Library/Test Library/Comics\godzilla rivals vs hedorah\vol 1\", "vol 1,godzilla rivals vs hedorah")]
    [InlineData(@"/manga/", @"/manga/Btooom!/Vol.1 Chapter 2/1.cbz", "Vol.1 Chapter 2,Btooom!")]
    [InlineData(@"C:/", @"C://Btooom!/Vol.1 Chapter 2/1.cbz", "Vol.1 Chapter 2,Btooom!")]
    [InlineData(@"C:\\", @"C://Btooom!/Vol.1 Chapter 2/1.cbz", "Vol.1 Chapter 2,Btooom!")]
    [InlineData(@"C://mount/gdrive/Library/Test Library/Comics", @"C://mount/gdrive/Library/Test Library/Comics/Dragon Age/Test", "Test,Dragon Age")]
    [InlineData(@"M:\", @"M:\Toukyou Akazukin\Vol. 01 Ch. 005.cbz", @"Toukyou Akazukin")]
    public void GetFoldersTillRoot_Test(string rootPath, string fullpath, string expectedArray)
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(rootPath);
        fileSystem.AddFile(fullpath, new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        var expected = expectedArray.Split(",");
        if (expectedArray.Equals(string.Empty))
        {
            expected = Array.Empty<string>();
        }
        Assert.Equal(expected, ds.GetFoldersTillRoot(rootPath, fullpath));
    }

    #endregion

    #region RemoveNonImages

    [Fact]
    public void RemoveNonImages()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(testDirectory);
        fileSystem.AddFile($"{testDirectory}file/data-0.txt", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}data-1.jpg", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}data-2.png", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}data-3.webp", new MockFileData("abc"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.RemoveNonImages($"{testDirectory}");
        Assert.False(fileSystem.FileExists($"{testDirectory}file/data-0.txt"));
        Assert.Equal(3, ds.GetFiles($"{testDirectory}", searchOption:SearchOption.AllDirectories).Count());
    }

    #endregion

    #region Flatten

    [Fact]
    public void Flatten_ShouldDoNothing()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(testDirectory);
        fileSystem.AddFile($"{testDirectory}data-1.jpg", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}data-2.png", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}data-3.webp", new MockFileData("abc"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.Flatten($"{testDirectory}");
        Assert.True(fileSystem.FileExists($"{testDirectory}data-1.jpg"));
        Assert.True(fileSystem.FileExists($"{testDirectory}data-2.png"));
        Assert.True(fileSystem.FileExists($"{testDirectory}data-3.webp"));
    }

    [Fact]
    public void Flatten_ShouldFlatten()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(testDirectory);
        fileSystem.AddFile($"{testDirectory}data-1.jpg", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}subdir/data-3.webp", new MockFileData("abc"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.Flatten($"{testDirectory}");
        Assert.Equal(2, ds.GetFiles(testDirectory).Count());
        Assert.False(fileSystem.FileExists($"{testDirectory}subdir/data-3.webp"));
        Assert.True(fileSystem.Directory.Exists($"{testDirectory}subdir/"));
    }

    [Fact]
    public void Flatten_ShouldFlatten_WithoutMacosx()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(testDirectory);
        fileSystem.AddFile($"{testDirectory}data-1.jpg", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}subdir/data-3.webp", new MockFileData("abc"));
        fileSystem.AddFile($"{testDirectory}__MACOSX/data-4.webp", new MockFileData("abc"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        ds.Flatten($"{testDirectory}");
        Assert.Equal(2, ds.GetFiles(testDirectory).Count());
        Assert.False(fileSystem.FileExists($"{testDirectory}data-4.webp"));
    }

    #endregion

    #region CheckWriteAccess

    [Fact]
    public async Task CheckWriteAccess_ShouldHaveAccess()
    {
        const string testDirectory = "/manga/";
        var fileSystem = new MockFileSystem();

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var hasAccess = await ds.CheckWriteAccess(ds.FileSystem.Path.Join(testDirectory, "bookmarks"));
        Assert.True(hasAccess);

        Assert.False(ds.FileSystem.Directory.Exists(ds.FileSystem.Path.Join(testDirectory, "bookmarks")));
        Assert.False(ds.FileSystem.File.Exists(ds.FileSystem.Path.Join(testDirectory, "bookmarks", "test.txt")));
    }


    #endregion

    #region GetHumanReadableBytes

    [Theory]
    [InlineData(1200, "1.17 KB")]
    [InlineData(1, "1 B")]
    [InlineData(10000000, "9.54 MB")]
    [InlineData(10000000000, "9.31 GB")]
    public void GetHumanReadableBytesTest(long bytes, string expected)
    {
        Assert.Equal(expected, DirectoryService.GetHumanReadableBytes(bytes));
    }
    #endregion

    #region ScanFiles

    [Fact]
    public Task ScanFiles_ShouldFindNoFiles_AllAreIgnored()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/.kavitaignore", new MockFileData("*.*"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        var globMatcher = new GlobMatcher();
        globMatcher.AddExclude("*.*");
        var allFiles = ds.ScanFiles("C:/Data/", API.Services.Tasks.Scanner.Parser.Parser.SupportedExtensions, globMatcher);

        Assert.Empty(allFiles);

        return Task.CompletedTask;
    }


    [Fact]
    public Task ScanFiles_ShouldFindNoNestedFiles_IgnoreNestedFiles()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/.kavitaignore", new MockFileData("**/Accel World/*"));
        fileSystem.AddFile("C:/Data/Hello.pdf", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        var globMatcher = new GlobMatcher();
        globMatcher.AddExclude("**/Accel World/*");
        var allFiles = ds.ScanFiles("C:/Data/", API.Services.Tasks.Scanner.Parser.Parser.SupportedExtensions, globMatcher);

        Assert.Single(allFiles); // Ignore files are not counted in files, only valid extensions

        return Task.CompletedTask;
    }


    [Fact]
    public Task ScanFiles_NestedIgnore_IgnoreNestedFilesInOneDirectoryOnly()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddDirectory("C:/Data/Specials/");
        fileSystem.AddDirectory("C:/Data/Specials/ArtBooks/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/.kavitaignore", new MockFileData("**/Accel World/*"));
        fileSystem.AddFile("C:/Data/Specials/.kavitaignore", new MockFileData("**/ArtBooks/*"));
        fileSystem.AddFile("C:/Data/Specials/Hi.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Specials/ArtBooks/art book 01.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Hello.pdf", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        var globMatcher = new GlobMatcher();
        globMatcher.AddExclude("**/Accel World/*");
        globMatcher.AddExclude("**/ArtBooks/*");
        var allFiles = ds.ScanFiles("C:/Data/", API.Services.Tasks.Scanner.Parser.Parser.SupportedExtensions, globMatcher);

        Assert.Equal(2, allFiles.Count); // Ignore files are not counted in files, only valid extensions

        return Task.CompletedTask;
    }


    [Fact]
    public Task ScanFiles_ShouldFindAllFiles()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.txt", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Nothing.pdf", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);

        var allFiles = ds.ScanFiles("C:/Data/", API.Services.Tasks.Scanner.Parser.Parser.SupportedExtensions);

        Assert.Equal(5, allFiles.Count);

        return Task.CompletedTask;
    }

    #endregion

    #region GetAllDirectories

    [Fact]
    public void GetAllDirectories_ShouldFindAllNestedDirectories()
    {
        const string testDirectory = "C:/manga/base/";
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(fileSystem.Path.Join(testDirectory, "folder 1"));
        fileSystem.AddDirectory(fileSystem.Path.Join(testDirectory, "folder 2"));
        fileSystem.AddDirectory(fileSystem.Path.Join(testDirectory, "folder 1", "A"));
        fileSystem.AddDirectory(fileSystem.Path.Join(testDirectory, "folder 1", "B"));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        Assert.Equal(2, ds.GetAllDirectories(fileSystem.Path.Join(testDirectory, "folder 1")).Count());
    }

    #endregion

    #region GetParentDirectory

    [Theory]
    [InlineData(@"C:/file.txt", "C:/")]
    [InlineData(@"C:/folder/file.txt", "C:/folder")]
    [InlineData(@"C:/folder/subfolder/file.txt", "C:/folder/subfolder")]
    public void GetParentDirectoryName_ShouldFindParentOfFiles(string path, string expected)
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { path, new MockFileData(string.Empty)}
        });

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        Assert.Equal(expected, ds.GetParentDirectoryName(path));
    }
    [Theory]
    [InlineData(@"C:/folder", "C:/")]
    [InlineData(@"C:/folder/subfolder", "C:/folder")]
    [InlineData(@"C:/folder/subfolder/another", "C:/folder/subfolder")]
    public void GetParentDirectoryName_ShouldFindParentOfDirectories(string path, string expected)
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(path);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        Assert.Equal(expected, ds.GetParentDirectoryName(path));
    }

    #endregion

    #region GetLastWriteTime

    [Fact]
    public void GetLastWriteTime_ShouldReturnMaxTime_IfNoFiles()
    {
        const string dir = "C:/manga/";
        var filesystem = new MockFileSystem();
        filesystem.AddDirectory("C:/");
        filesystem.AddDirectory(dir);
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);

        Assert.Equal(DateTime.MaxValue, ds.GetLastWriteTime(dir));
    }

    #endregion
}
