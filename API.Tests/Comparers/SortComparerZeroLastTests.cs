using System.Linq;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers;

public class SortComparerZeroLastTests
{
    [Theory]
    [InlineData(new[] {0, 1, 2,}, new[] {1, 2, 0})]
    [InlineData(new[] {3, 1, 2}, new[] {1, 2, 3})]
    [InlineData(new[] {0, 0, 1}, new[] {1, 0, 0})]
    public void SortComparerZeroLastTest(int[] input, int[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, ChapterSortComparerSpecialsLast.Default).ToArray());
    }
}
