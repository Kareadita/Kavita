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

    #endregion
}
