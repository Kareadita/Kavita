using System.IO.Abstractions.TestingHelpers;
using Kavita.API.Services.Helpers;
using Kavita.Database.Tests;
using Kavita.Services.Helpers;

namespace Kavita.Services.Tests.Helpers;

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

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, true, false)]
    public void ShouldUpdateCoverImage_CoverExists(bool sourceChanged, bool forceUpdate, bool isCoverLocked, bool expected)
    {
        Assert.Equal(expected, _cacheHelper.ShouldUpdateCoverImage(_testCoverPath, sourceChanged, forceUpdate, isCoverLocked));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldUpdateCoverImage_CoverMissing_UpdatesEvenWhenLocked(bool isCoverLocked)
    {
        Assert.True(_cacheHelper.ShouldUpdateCoverImage(Path.Join(TestCoverImageDirectory, "missing.jpg"), false, false, isCoverLocked));
    }
}
