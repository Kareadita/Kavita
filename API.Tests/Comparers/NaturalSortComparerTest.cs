using System;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers
{
    public class NaturalSortComparerTest
    {
        [Theory]
        [InlineData(
            new[] {"x1.jpg", "x10.jpg", "x3.jpg", "x4.jpg", "x11.jpg"}, 
            new[] {"x1.jpg", "x3.jpg", "x4.jpg", "x10.jpg", "x11.jpg"}
        )]
        [InlineData(
            new[] {"Beelzebub_153b_RHS.zip", "Beelzebub_01_[Noodles].zip",}, 
            new[] {"Beelzebub_01_[Noodles].zip", "Beelzebub_153b_RHS.zip"}
        )]
        public void TestNaturalSortComparer(string[] input, string[] expected)
        {
            NaturalSortComparer nc = new NaturalSortComparer();
            Array.Sort(input, nc);

            var i = 0;
            foreach (var s in input)
            {
                Assert.Equal(s, expected[i]);
                i++;
            }
        }
    }
}