using System;
using API.Extensions;
using Xunit;

namespace API.Tests.Extensions;

public class VersionHelperTests
{
    [Fact]
    public void CompareWithoutRevision_ShouldReturnTrue_WhenMajorMinorBuildMatch()
    {
        // Arrange
        var v1 = new Version(1, 2, 3, 4);
        var v2 = new Version(1, 2, 3, 5);

        // Act
        var result = v1.CompareWithoutRevision(v2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CompareWithoutRevision_ShouldHandleBuildlessVersions()
    {
        // Arrange
        var v1 = new Version(1, 2);
        var v2 = new Version(1, 2);

        // Act
        var result = v1.CompareWithoutRevision(v2);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(1, 2, 3, 1, 2, 4)]
    [InlineData(1, 2, 3, 1, 2, 0)]
    public void CompareWithoutRevision_ShouldReturnFalse_WhenBuildDiffers(
        int major1, int minor1, int build1,
        int major2, int minor2, int build2)
    {
        var v1 = new Version(major1, minor1, build1);
        var v2 = new Version(major2, minor2, build2);

        var result = v1.CompareWithoutRevision(v2);

        Assert.False(result);
    }

    [Theory]
    [InlineData(1, 2, 3, 1, 3, 3)]
    [InlineData(1, 2, 3, 1, 0, 3)]
    public void CompareWithoutRevision_ShouldReturnFalse_WhenMinorDiffers(
        int major1, int minor1, int build1,
        int major2, int minor2, int build2)
    {
        var v1 = new Version(major1, minor1, build1);
        var v2 = new Version(major2, minor2, build2);

        var result = v1.CompareWithoutRevision(v2);

        Assert.False(result);
    }

    [Theory]
    [InlineData(1, 2, 3, 2, 2, 3)]
    [InlineData(1, 2, 3, 0, 2, 3)]
    public void CompareWithoutRevision_ShouldReturnFalse_WhenMajorDiffers(
        int major1, int minor1, int build1,
        int major2, int minor2, int build2)
    {
        var v1 = new Version(major1, minor1, build1);
        var v2 = new Version(major2, minor2, build2);

        var result = v1.CompareWithoutRevision(v2);

        Assert.False(result);
    }
}
