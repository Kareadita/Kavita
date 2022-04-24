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
    [InlineData(
        new[] {"x1.0.jpg", "0.5.jpg", "0.3.jpg"},
        new[] {"0.3.jpg", "0.5.jpg", "x1.0.jpg",}
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
