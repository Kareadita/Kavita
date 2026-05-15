using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Database.Tests;
using Kavita.Models.DTOs.BookUpload;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Uploads;

public class BookUploadServiceTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    [Fact]
    public async Task UploadFilesAsync_ShouldUploadAndQueueScan()
    {
        var (service, taskScheduler, fileSystem, libraryId) = await CreateService();

        var response = await service.UploadFilesAsync(new BookUploadRequestDto
        {
            LibraryId = libraryId,
            LibraryFolder = DataDirectory,
            ConflictMode = BookUploadConflictMode.Reject
        }, [CreateUploadFile("Love Hina Vol. 01.cbz")]);

        Assert.True(response.Success);
        Assert.True(fileSystem.File.Exists(Path.Join(DataDirectory, "Love Hina Vol. 01", "Love Hina Vol. 01.cbz")));
        taskScheduler.Received(1).ScanFolder(Arg.Any<string>());
    }

    [Fact]
    public async Task UploadFilesAsync_ShouldRejectPathTraversal()
    {
        var (service, taskScheduler, fileSystem, libraryId) = await CreateService();

        var response = await service.UploadFilesAsync(new BookUploadRequestDto
        {
            LibraryId = libraryId,
            LibraryFolder = DataDirectory,
            TargetFolderName = "../evil",
            ConflictMode = BookUploadConflictMode.Reject
        }, [CreateUploadFile("Love Hina Vol. 01.cbz")]);

        Assert.False(response.Success);
        Assert.False(fileSystem.File.Exists(Path.Join(DataDirectory, "evil", "Love Hina Vol. 01.cbz")));
        taskScheduler.DidNotReceive().ScanFolder(Arg.Any<string>());
    }

    [Fact]
    public async Task UploadFilesAsync_ShouldRenameDuplicateWhenRequested()
    {
        var (service, _, fileSystem, libraryId) = await CreateService();
        fileSystem.AddFile(Path.Join(DataDirectory, "Love Hina", "Love Hina Vol. 01.cbz"), new MockFileData("existing"));

        var response = await service.UploadFilesAsync(new BookUploadRequestDto
        {
            LibraryId = libraryId,
            LibraryFolder = DataDirectory,
            TargetFolderName = "Love Hina",
            ConflictMode = BookUploadConflictMode.Rename
        }, [CreateUploadFile("Love Hina Vol. 01.cbz")]);

        Assert.True(response.Success);
        Assert.True(fileSystem.File.Exists(Path.Join(DataDirectory, "Love Hina", "Love Hina Vol. 01 (1).cbz")));
    }

    [Fact]
    public async Task UploadFilesAsync_ShouldAllowEllipsisInFileName()
    {
        var (service, _, fileSystem, libraryId) = await CreateService();
        const string FileName = "Half-Hour History...Special Edition.cbz";

        var response = await service.UploadFilesAsync(new BookUploadRequestDto
        {
            LibraryId = libraryId,
            LibraryFolder = DataDirectory,
            ConflictMode = BookUploadConflictMode.Reject
        }, [CreateUploadFile(FileName)]);

        Assert.True(response.Success);
        Assert.True(fileSystem.File.Exists(Path.Join(DataDirectory, "Half-Hour History...Special Edition", FileName)));
    }

    [Fact]
    public async Task UploadFilesAsync_ShouldRejectDisabledFileType()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var library = await context.Library
            .Include(l => l.LibraryFileTypes)
            .SingleAsync();

        context.Set<LibraryFileTypeGroup>().RemoveRange(library.LibraryFileTypes);
        library.LibraryFileTypes = [new LibraryFileTypeGroup {LibraryId = library.Id, FileTypeGroup = FileTypeGroup.Epub}];
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var fileSystem = CreateFileSystem();
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var taskScheduler = Substitute.For<ITaskScheduler>();
        var service = new BookUploadService(unitOfWork, directoryService, taskScheduler,
            Substitute.For<ILogger<BookUploadService>>());

        var response = await service.UploadFilesAsync(new BookUploadRequestDto
        {
            LibraryId = library.Id,
            LibraryFolder = DataDirectory,
            ConflictMode = BookUploadConflictMode.Reject
        }, [CreateUploadFile("Love Hina Vol. 01.cbz")]);

        Assert.False(response.Success);
        Assert.Contains(response.Files, file => file.Error == "File type is not enabled for this library");
        taskScheduler.DidNotReceive().ScanFolder(Arg.Any<string>());
    }

    private async Task<(BookUploadService Service, ITaskScheduler TaskScheduler, MockFileSystem FileSystem, int LibraryId)> CreateService()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var fileSystem = CreateFileSystem();
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var taskScheduler = Substitute.For<ITaskScheduler>();
        var service = new BookUploadService(unitOfWork, directoryService, taskScheduler,
            Substitute.For<ILogger<BookUploadService>>());
        var libraryId = context.Library.Single().Id;

        return (service, taskScheduler, fileSystem, libraryId);
    }

    private static BookUploadFile CreateUploadFile(string fileName)
    {
        return new BookUploadFile(fileName, 4,
            () => new MemoryStream(Encoding.UTF8.GetBytes("book")));
    }
}
