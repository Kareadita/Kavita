using System.IO.Abstractions.TestingHelpers;
using Kavita.Database.Tests;

namespace Kavita.Services.Tests;

public class FileSystemTests : AbstractFsTest
{
    private static readonly string FilePath = Root + "myfile.txt";

    [Fact]
    public void FileHasNotBeenModifiedSinceCreation()
    {
        var file = new MockFileData("Testing is meh.")
        {
            LastWriteTime = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(1))
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { FilePath, file }
        });

        var fileService = new FileService(fileSystem);

        Assert.False(fileService.HasFileBeenModifiedSince(FilePath,DateTime.Now));
    }

    [Fact]
    public void FileHasBeenModifiedSinceCreation()
    {
        var file = new MockFileData("Testing is meh.")
        {
            LastWriteTime = DateTimeOffset.Now
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { FilePath, file }
        });

        var fileService = new FileService(fileSystem);

        Assert.True(fileService.HasFileBeenModifiedSince(FilePath,DateTime.Now.Subtract(TimeSpan.FromMinutes(1))));
    }
}
