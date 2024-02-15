using System.Linq;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers;

public class ChapterSortComparerSpecialsLastTest
{
    [Theory]
    [InlineData(new[] {1, 2, 0}, new[] {1, 2, 0})]
    [InlineData(new[] {3, 1, 2}, new[] {1, 2, 3})]
    [InlineData(new[] {1, 0, 0}, new[] {1, 0, 0})]
    public void ChapterSortTest(int[] input, int[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, new ChapterSortComparerSpecialsLast()).ToArray());
    }

}
