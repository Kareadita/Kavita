using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using Xunit;

namespace API.Tests.Helpers;

public class CacheHelperTests: AbstractFsTest
{
    private static readonly string TestCoverImageDirectory = Root;
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
    [InlineData(null, false)]
    public void CoverImageExists_DoesFileExist(string coverImage, bool exists)
    {
        Assert.Equal(exists, _cacheHelper.CoverImageExists(coverImage));
    }

    [Fact]
    public void CoverImageExists_DoesFileExistRoot()
    {
        Assert.False(_cacheHelper.CoverImageExists(Root));
    }

    [Fact]
    public void CoverImageExists_FileExists()
    {
        Assert.True(_cacheHelper.CoverImageExists(Path.Join(TestCoverImageDirectory, TestCoverArchive)));
    }

    [Fact]
    public void ShouldUpdateCoverImage_OnFirstRun()
    {

        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.True(_cacheHelper.ShouldUpdateCoverImage(null, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetNotLocked()
    {
        // Represents first run
        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, false));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetNotLocked_2()
    {
        // Represents first run
        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now,
            false, false));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetLocked()
    {
        // Represents first run
        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(DateTime.Now)
            .Build();
        Assert.False(_cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, DateTime.Now.Subtract(TimeSpan.FromMinutes(1)),
            false, true));
    }

    [Fact]
    public void ShouldUpdateCoverImage_ShouldNotUpdateOnSecondRunWithCoverImageSetLocked_Modified()
    {
        // Represents first run
        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
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
        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(DateTime.Now.Subtract(TimeSpan.FromMinutes(1)))
            .Build();

        Assert.True(cacheHelper.ShouldUpdateCoverImage(_testCoverPath, file, created,
            false, false));
    }

    [Fact]
    public void HasFileNotChangedSinceCreationOrLastScan_NotChangedSinceCreated()
    {
        var now = DateTimeOffset.Now;
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime =now,
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var chapter = new ChapterBuilder("1")
            .WithLastModified(now.DateTime)
            .WithCreated(now.DateTime)
            .Build();

        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(now.DateTime)
            .Build();
        Assert.True(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

    [Fact]
    public void HasFileNotChangedSinceCreationOrLastScan_NotChangedSinceLastModified()
    {
        var now = DateTimeOffset.Now;
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = now,
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var chapter = new ChapterBuilder("1")
            .WithLastModified(now.DateTime)
            .WithCreated(now.DateTime)
            .Build();

        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(now.DateTime)
            .Build();

        Assert.True(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

    [Fact]
    public void HasFileNotChangedSinceCreationOrLastScan_NotChangedSinceLastModified_ForceUpdate()
    {
        var now = DateTimeOffset.Now;
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = now.DateTime,
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { Path.Join(TestCoverImageDirectory, TestCoverArchive), filesystemFile },
            { Path.Join(TestCoverImageDirectory, TestCoverImageFile), filesystemFile }
        });

        var fileService = new FileService(fileSystem);
        var cacheHelper = new CacheHelper(fileService);

        var chapter = new ChapterBuilder("1")
            .WithLastModified(now.DateTime)
            .WithCreated(now.DateTime)
            .Build();

        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(now.DateTime)
            .Build();
        Assert.False(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, true, file));
    }

    [Fact]
    public void IsFileUnmodifiedSinceCreationOrLastScan_ModifiedSinceLastScan()
    {
        var now = DateTimeOffset.Now;
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime = now.DateTime,
            CreationTime = now.DateTime
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

        var file = new MangaFileBuilder(Path.Join(TestCoverImageDirectory, TestCoverArchive), MangaFormat.Archive)
            .WithLastModified(now.DateTime)
            .Build();
        Assert.False(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

    [Fact]
    public void HasFileNotChangedSinceCreationOrLastScan_ModifiedSinceLastScan_ButLastModifiedSame()
    {
        var now = DateTimeOffset.Now;
        var filesystemFile = new MockFileData("")
        {
            LastWriteTime =now.DateTime
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
            .WithLastModified(now.DateTime)
            .Build();

        Assert.False(cacheHelper.IsFileUnmodifiedSinceCreationOrLastScan(chapter, false, file));
    }

}
