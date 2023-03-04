﻿using System;
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
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = DateTime.Now
        };
        Assert.True(_cacheHelper.ShouldUpdateCoverImage(null, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false));
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
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetNotLocked_2()
    {
        // Represents first run
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = DateTime.Now
        };
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now,
            false, false));
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
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, true));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetLocked_Modified()
    {
        // Represents first run
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = DateTime.Now
        };
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
        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = DateTime.Now.Subtract(TimeSpan.FromMinutes(1))
        };
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

        var chapter = new Chapter()
        {
            Number = "1",
            Range = "1",
            Created = filesystemFile.LastWriteTime.DateTime,
            LastModified = filesystemFile.LastWriteTime.DateTime
        };

        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = filesystemFile.LastWriteTime.DateTime
        };
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

        var chapter = new Chapter()
        {
            Number = "1",
            Range = "1",
            Created = filesystemFile.LastWriteTime.DateTime,
            LastModified = filesystemFile.LastWriteTime.DateTime
        };

        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = filesystemFile.LastWriteTime.DateTime
        };
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

        var chapter = new Chapter()
        {
            Number = "1",
            Range = "1",
            Created = filesystemFile.LastWriteTime.DateTime,
            LastModified = filesystemFile.LastWriteTime.DateTime
        };

        var file = new MangaFile()
        {
            FilePath = TestCoverArchive,
            LastModified = filesystemFile.LastWriteTime.DateTime
        };
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

        var chapter = new Chapter()
        {
            Number = "1",
            Range = "1",
            Created = DateTime.Now.Subtract(TimeSpan.FromMinutes(10)),
            LastModified = DateTime.Now.Subtract(TimeSpan.FromMinutes(10))
        };

        var file = new MangaFile()
        {
            FilePath = Path.Join(TestCoverImageDirectory, TestCoverArchive),
            LastModified = filesystemFile.LastWriteTime.DateTime
        };
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

        var chapter = new Chapter()
        {
            Number = "1",
            Range = "1",
            Created = DateTime.Now.Subtract(TimeSpan.FromMinutes(10)),
            LastModified = DateTime.Now
        };

        var file = new MangaFile()
        {
            FilePath = Path.Join(TestCoverImageDirectory, TestCoverArchive),
            LastModified = filesystemFile.LastWriteTime.DateTime
        };
        Assert.False(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

}
