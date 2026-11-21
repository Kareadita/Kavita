using Kavita.Common.Extensions;

namespace Kavita.Common.Tests.Extensions;

public class PathExtensionsTests
{
    #region GetFullPathWithoutExtension

    [Theory]
    [InlineData("foo.png", "foo")]
    [InlineData("c:/directory/foo.png", "c:/directory/foo")]
    public void GetFullPathWithoutExtension_Test(string input, string expected)
    {
        Assert.Equal(Path.GetFullPath(expected), input.GetFullPathWithoutExtension());
    }

    #endregion

    #region IsInsideFolder

    [Theory]
    // The drive root in every spelling is the folder itself, never inside it
    [InlineData("M:", "M:/", false, true)]
    [InlineData("M:/", "M:/", false, true)]
    [InlineData(@"M:\", "M:/", false, true)]
    [InlineData("M:", @"M:\", false, true)]
    [InlineData("/", "/", false, true)]
    [InlineData("/manga", "/manga/", false, true)]
    // Real children
    [InlineData("M:/Accel World", "M:/", true, true)]
    [InlineData("M:/Accel World", "M:", true, true)]
    [InlineData(@"M:\Accel World\v01.cbz", @"M:\", true, true)]
    [InlineData("/manga/Accel World", "/", true, true)]
    [InlineData("/manga/Accel World/", "/manga", true, true)]
    [InlineData("B:/Fiction/Author/Book.epub", "B:/Fiction", true, true)]
    // Siblings that share a prefix
    [InlineData("M:/Manga2", "M:/Manga", false, false)]
    [InlineData("B:/Fiction2/Author/Book.epub", "B:/Fiction", false, false)]
    [InlineData("M:/Accel World  Dural - Magisa Garden/x.cbz", "M:/Accel World", false, false)]
    // Above the folder, or somewhere else
    [InlineData("M:/", "M:/Manga", false, false)]
    [InlineData("N:/Manga", "M:/Manga", false, false)]
    // Case is compared exactly
    [InlineData("m:/manga/Accel World", "M:/Manga", false, false)]
    // Missing values
    [InlineData("", "M:/", false, false)]
    [InlineData(null, "M:/", false, false)]
    [InlineData("M:/Manga", "", false, false)]
    [InlineData("M:/Manga", null, false, false)]
    public void IsInsideFolder_Test(string? path, string? folder, bool inside, bool sameOrInside)
    {
        Assert.Equal(inside, path.IsInsideFolder(folder));
        Assert.Equal(sameOrInside, path.IsSameOrInsideFolder(folder));
    }

    #endregion

    #region DeepestContainingFolder

    [Theory]
    [InlineData("B:/Fiction/Other/a.epub", "B:/Fiction")]
    [InlineData("B:/Other/a.epub", "B:/")]
    [InlineData("B:/Fiction", "B:/Fiction")]
    [InlineData("M:/Manga2/a.cbz", null)]
    [InlineData("N:/a.cbz", null)]
    public void DeepestContainingFolder_Test(string path, string? expected)
    {
        Assert.Equal(expected, path.DeepestContainingFolder(["B:/", "B:/Fiction", "M:/Manga"]));
    }

    #endregion

    #region FolderOf

    [Theory]
    [InlineData("B:/Fiction/Martha Wells/The Murderbot Diaries/Fugitive Telemetry.epub", "B:/Fiction/Martha Wells/The Murderbot Diaries")]
    [InlineData("M:/a.epub", "M:")]
    [InlineData("/manga/a.cbz", "/manga")]
    [InlineData("/a.cbz", "/")]
    [InlineData("a.cbz", "")]
    public void FolderOf_Test(string path, string expected)
    {
        Assert.Equal(expected, path.FolderOf());
    }

    [Theory]
    [InlineData("M:/a.epub", "M:/")]
    [InlineData("/a.cbz", "/")]
    [InlineData("/manga/a.cbz", "/manga")]
    public void FolderOf_IsInItsOwnFolder(string path, string folder)
    {
        Assert.True(path.FolderOf().IsSameOrInsideFolder(folder));
    }

    #endregion

    #region SanitizeFileName

    [Theory]
    [InlineData("Civil War.cbl", "Civil War.cbl")]
    [InlineData("Marvel – 日本.cbl", "Marvel – 日本.cbl")]
    [InlineData("a/b.cbl", "b.cbl")]
    [InlineData(@"..\..\kavita.cbl", "kavita.cbl")]
    [InlineData("../../config/kavita.db", "kavita.db")]
    [InlineData("list\n.cbl", "list.cbl")]
    [InlineData("..", "")]
    [InlineData(".", "")]
    [InlineData("folder/", "")]
    [InlineData("", "")]
    public void SanitizeFileName_KeepsOnlyABareName(string input, string expected)
    {
        Assert.Equal(expected, input.SanitizeFileName());
    }

    #endregion

    #region UrlFilePath

    [Theory]
    [InlineData("https://x.invalid/lists/Civil%20War.cbl?dl=1", "/lists/Civil War.cbl")]
    [InlineData("https://x.invalid/a%2Fb.cbl", "/a/b.cbl")]
    [InlineData("https://x.invalid/", "/")]
    [InlineData("not a url", "")]
    [InlineData("", "")]
    public void UrlFilePath_ReturnsTheDecodedPath(string input, string expected)
    {
        Assert.Equal(expected, input.UrlFilePath());
    }

    [Fact]
    public void UrlFilePath_EncodedTraversal_SanitizesToABareName()
    {
        Assert.Equal("kavita.cbl", "https://x.invalid/a/..%2F..%2Fkavita.cbl".UrlFilePath().SanitizeFileName());
    }

    #endregion
}
