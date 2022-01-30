using System;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers;

public class NumericComparerTests
{
    [Theory]
    [InlineData(
        new[] {"x1.jpg", "x10.jpg", "x3.jpg", "x4.jpg", "x11.jpg"},
        new[] {"x1.jpg", "x3.jpg", "x4.jpg", "x10.jpg", "x11.jpg"}
    )]
    public void NumericComparerTest(string[] input, string[] expected)
    {
        var nc = new NumericComparer();
        Array.Sort(input, nc);

        var i = 0;
        foreach (var s in input)
        {
            Assert.Equal(s, expected[i]);
            i++;
        }
    }
}
