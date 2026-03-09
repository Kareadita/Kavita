using Kavita.Services.Comparators;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests.Comparers;

public class ChapterSortComparerDefaultLastTest
{
    [Theory]
    [InlineData(new[] {1, 2, Parser.DefaultChapterNumber}, new[] {1, 2, Parser.DefaultChapterNumber})]
    [InlineData(new[] {3, 1, 2}, new[] {1, 2, 3})]
    [InlineData(new[] {1, Parser.DefaultChapterNumber, Parser.DefaultChapterNumber}, new[] {1, Parser.DefaultChapterNumber, Parser.DefaultChapterNumber})]
    [InlineData(new[] {Parser.DefaultChapterNumber, 1}, new[] {1, Parser.DefaultChapterNumber})]
    public void ChapterSortTest(int[] input, int[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, new ChapterSortComparerDefaultLast()).ToArray());
    }

}
