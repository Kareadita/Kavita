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
}
