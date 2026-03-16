using Kavita.Services.Comparators;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests.Comparers;

public class SortComparerZeroLastTests
{
    [Theory]
    [InlineData(new[] {Parser.DefaultChapterNumber, 1, 2,}, new[] {1, 2, Parser.DefaultChapterNumber})]
    [InlineData(new[] {3, 1, 2}, new[] {1, 2, 3})]
    [InlineData(new[] {Parser.DefaultChapterNumber, Parser.DefaultChapterNumber, 1}, new[] {1, Parser.DefaultChapterNumber, Parser.DefaultChapterNumber})]
    public void SortComparerZeroLastTest(int[] input, int[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, ChapterSortComparerDefaultLast.Default).ToArray());
    }
}
