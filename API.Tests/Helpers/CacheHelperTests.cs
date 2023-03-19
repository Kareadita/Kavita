using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using Xunit;

namespace API.Tests.Helpers;

public class CacheHelperTests
{
    private const string TestCoverImageDirectory = @"c:\";
    private const string TestCoverImageFile = "thumbnail.jpg";
    private readonly string _testCoverPath = Path.Join(TestCoverImageDirectory, TestCoverImageFile);
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
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), file },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), file }
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

        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.True(_cacheHelper.ShouldUpdateCoverImage(null, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetNotLocked()
    {
        // Represents first run
        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetNotLocked_2()
    {
        // Represents first run
        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now,
            false, false));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetLocked()
    {
        // Represents first run
        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, true));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetLocked_Modified()
    {
        // Represents first run
        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, true));
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
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var created = DateTime.Now.Subtract(TimeSpan.FromHours(1));
        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(DateTime.Now.Subtract(TimeSpan.FromMinutes(1)))
            .Build();

        Assert.True(cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, created,
            false, false));
    }

    [Fact]
    public void HasFileNotChangedSinceCreationOrLastScan_NotChangedSinceCreated()
    {
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = DateTimeOffset.Now
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var chapter = new ChapterBuilder("1")
            .WithLastModified(filesystemFile.LastWriteTime.DateTime)
            .WithCreated(filesystemFile.LastWriteTime.DateTime)
            .Build();

        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(filesystemFile.LastWriteTime.DateTime)
            .Build();
        Assert.True(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

    [Fact]
    public void HasFileNotChangedSinceCreationOrLastScan_NotChangedSinceLastModified()
    {
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = DateTimeOffset.Now
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var chapter = new ChapterBuilder("1")
            .WithLastModified(filesystemFile.LastWriteTime.DateTime)
            .WithCreated(filesystemFile.LastWriteTime.DateTime)
            .Build();

        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(filesystemFile.LastWriteTime.DateTime)
            .Build();

        Assert.True(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

    [Fact]
    public void HasFileNotChangedSinceCreationOrLastScan_NotChangedSinceLastModified_ForceUpdate()
    {
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = DateTimeOffset.Now
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var chapter = new ChapterBuilder("1")
            .WithLastModified(filesystemFile.LastWriteTime.DateTime)
            .WithCreated(filesystemFile.LastWriteTime.DateTime)
            .Build();

        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(filesystemFile.LastWriteTime.DateTime)
            .Build();
        Assert.False(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, true, file));
    }

    [Fact]
    public void IsFileUnmodifiedSinceCreationOrLastScan_ModifiedSinceLastScan()
    {
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = DateTimeOffset.Now,
            CreationTime = DateTimeOffset.Now
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var chapter = new ChapterBuilder("1")
            .WithLastModified(DateTime.Now.Subtract(TimeSpan.FromMinutes(10)))
            .WithCreated(DateTime.Now.Subtract(TimeSpan.FromMinutes(10)))
            .Build();

        var file = new MangaFileBuilder(TestCoverArchive, MangaFormat.Archive)
            .WithLastModified(filesystemFile.LastWriteTime.DateTime)
            .Build();
        Assert.False(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

    [Fact]
    public void HasFileNotChangedSinceCreationOrLastScan_ModifiedSinceLastScan_ButLastModifiedSame()
    {
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = DateTimeOffset.Now
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var chapter = new ChapterBuilder("1")
            .WithLastModified(DateTime.Now)
            .WithCreated(DateTime.Now.Subtract(TimeSpan.FromMinutes(10)))
            .Build();

        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(filesystemFile.LastWriteTime.DateTime)
            .Build();

        Assert.False(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

}
