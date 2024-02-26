using System.Linq;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers;

public class ChapterSortComparerDefaultFirstTests
{
    [Theory]
    [InlineData(new[] {1, 2, 0}, new[] {0, 1, 2,})]
    [InlineData(new[] {3, 1, 2}, new[] {1, 2, 3})]
    [InlineData(new[] {1, 0, 0}, new[] {0, 0, 1})]
    public void ChapterSortComparerZeroFirstTest(int[] input, int[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, new ChapterSortComparerDefaultFirst()).ToArray());
    }

    [Theory]
    [InlineData(new [] {1.0f, 0.5f, 0.3f}, new [] {0.3f, 0.5f, 1.0f})]
    public void ChapterSortComparerZeroFirstTest_Doubles(float[] input, float[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, new ChapterSortComparerDefaultFirst()).ToArray());
    }
}
