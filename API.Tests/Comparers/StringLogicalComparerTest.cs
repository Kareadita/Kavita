using System;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers;

public class StringLogicalComparerTest
{
    [Theory]
    [InlineData(
        new[] {"x1.jpg", "x10.jpg", "x3.jpg", "x4.jpg", "x11.jpg"},
        new[] {"x1.jpg", "x3.jpg", "x4.jpg", "x10.jpg", "x11.jpg"}
    )]
    [InlineData(
        new[] {"a.jpg", "aaa.jpg", "1.jpg", },
        new[] {"1.jpg", "a.jpg", "aaa.jpg"}
    )]
    [InlineData(
        new[] {"a.jpg", "aaa.jpg", "1.jpg", "!cover.png"},
        new[] {"!cover.png", "1.jpg", "a.jpg", "aaa.jpg"}
    )]
    public void StringComparer(string[] input, string[] expected)
    {
        Array.Sort(input, StringLogicalComparer.Compare);

        var i = 0;
        foreach (var s in input)
        {
            Assert.Equal(s, expected[i]);
            i++;
        }
    }
}
