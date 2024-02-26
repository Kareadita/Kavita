using System.Linq;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers;

public class ChapterSortComparerDefaultLastTest
{
    [Theory]
    [InlineData(new[] {1, 2, API.Services.Tasks.Scanner.Parser.Parser.DefaultChapterNumber}, new[] {1, 2, API.Services.Tasks.Scanner.Parser.Parser.DefaultChapterNumber})]
    [InlineData(new[] {3, 1, 2}, new[] {1, 2, 3})]
    [InlineData(new[] {1, API.Services.Tasks.Scanner.Parser.Parser.DefaultChapterNumber, API.Services.Tasks.Scanner.Parser.Parser.DefaultChapterNumber}, new[] {1, API.Services.Tasks.Scanner.Parser.Parser.DefaultChapterNumber, API.Services.Tasks.Scanner.Parser.Parser.DefaultChapterNumber})]
    [InlineData(new[] {API.Services.Tasks.Scanner.Parser.Parser.DefaultChapterNumber, 1}, new[] {1, API.Services.Tasks.Scanner.Parser.Parser.DefaultChapterNumber})]
    public void ChapterSortTest(int[] input, int[] expected)
    {
        Assert.Equal(expected, input.OrderBy(f => f, new ChapterSortComparerDefaultLast()).ToArray());
    }

}
