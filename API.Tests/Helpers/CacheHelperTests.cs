using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using API.Entities;
using API.Helpers;
using API.Services;
using Xunit;

namespace API.Tests.Helpers;

public class CacheHelperTests
{
    //private static readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
    private readonly string _testCoverImageDirectory = @"c:\";
    //private readonly string ExistingCoverImage = Path.Join(Directory.GetCurrentDirectory(), @"../../../Services/Test Data/ArchiveService/CoverImages", "thumbnail.jpg");
    private const string TestCoverImageFile = "thumbnail.jpg";
    private const string TestCoverArchive = @"file in folder.zip";
    private readonly ICacheHelper _cacheHelper;

    public CacheHelperTests()
    {
        var file = new MockFileData("")
        {
            LastWriteTime = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(1))
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(_testCoverImageDirectory, TestCoverArchive), file },
            { Path.Join(_testCoverImageDirectory, TestCoverImageFile), file }
        });

        var fileService = new FileService(fileSystem);
        _cacheHelper = new CacheHelper(fileService);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("C:/", false)]
    [InlineData(null, false)]
    public void CoverImageExists_DoesFileExist(string coverImage, bool exists)
    {
        Assert.Equal(exists, _cacheHelper.CoverImageExists(coverImage));
    }

    [Fact]
    public void CoverImageExists_FileExists()
    {
        Assert.True(_cacheHelper.CoverImageExists(TestCoverArchive));
    }

    [Fact]
    public void ShouldUpdateCoverImage_OnFirstRun()
    {
        // Represents first run
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = DateTime.Now
        };
        Assert.True(_cacheHelper.ShouldUpdateCoverImage(null, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false, _testCoverImageDirectory));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetNotLocked()
    {
        // Represents first run
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = DateTime.Now
        };
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(TestCoverImageFile, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false, _testCoverImageDirectory));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetLocked()
    {
        // Represents first run
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = DateTime.Now
        };
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(TestCoverImageFile, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, true, _testCoverImageDirectory));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetLocked_Modified()
    {
        // Represents first run
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive, // TODO: This needs to somehow be touched
            LastModified = DateTime.Now
        };
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(TestCoverImageFile, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, true, _testCoverImageDirectory));
    }

    [Fact]
    public void ShouldUpdateCoverImage_CoverImageSetAndReplaced_Modified()
    {
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = DateTimeOffset.Now
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(_testCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(_testCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        // Represents first run
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = DateTime.Now.Subtract(TimeSpan.FromMinutes(1))
        };
        Assert.True(cacheHelper.ShouldUpdateCoverImage(TestCoverImageFile, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false, _testCoverImageDirectory));
    }
}
