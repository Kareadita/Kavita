using System.Linq;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers;

public class ChapterSortComparerSpecialsFirstTests
{
    [Theory]
    [InlineData(new[] {1, 2, 0}, new[] {0, 1, 2,})]
    [InlineData(new[] {3, 1, 2}, new[] {1, 2, 3})]
    [InlineData(new[] {1, 0, 0}, new[] {0, 0, 1})]
    public void ChapterSortComparerZeroFirstTest(int[] input, int[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, new ChapterSortComparerSpecialsFirst()).ToArray());
    }

    [Theory]
    [InlineData(new[] {1.0, 0.5, 0.3}, new[] {0.3, 0.5, 1.0})]
    public void ChapterSortComparerZeroFirstTest_Doubles(double[] input, double[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, new ChapterSortComparerSpecialsFirst()).ToArray());
    }
}
