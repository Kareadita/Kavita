using System.IO;
using Xunit;
using API.Extensions;

namespace API.Tests.Extensions;

public class PathExtensionsTests
{
    #region GetFullPathWithoutExtension

    [Theory]
    [InlineData("joe.png", "joe")]
    [InlineData("c:/directory/joe.png", "c:/directory/joe")]
    public void GetFullPathWithoutExtension_Test(string input, string expected)
    {
        Assert.Equal(Path.GetFullPath(expected), input.GetFullPathWithoutExtension());
    }

    [Theory]
    [InlineData("1/cover.jpeg", "1")]
    [InlineData("01/cover.jpeg", "01")]
    [InlineData("01/images/cover.jpeg", "01")]
    public void GetFirstSegmentSpanTests(string input, string expected)
    {
        Assert.Equal(expected, input.GetFirstSegmentSpan().ToString());
    }

    [Theory]
    [InlineData("1/cover.jpeg")]
    [InlineData("01/cover.jpeg")]
    [InlineData("01/images/cover.jpeg")]
    public void GetLastSegmentSpanTests(string input)
    {
        Assert.Equal("cover.jpeg", input.GetLastSegmentSpan().ToString());
    }

    #endregion
}
