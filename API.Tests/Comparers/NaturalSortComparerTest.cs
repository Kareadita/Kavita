using System;
using System.Linq;
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
        [InlineData(
            new[] {"[SCX-Scans]_Vandread_v02_Act02.zip", "[SCX-Scans]_Vandread_v02_Act01.zip",}, 
            new[] {"[SCX-Scans]_Vandread_v02_Act01.zip", "[SCX-Scans]_Vandread_v02_Act02.zip",}
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
        
        
        [Theory]
        [InlineData(
            new[] {"x1.jpg", "x10.jpg", "x3.jpg", "x4.jpg", "x11.jpg"}, 
            new[] {"x1.jpg", "x3.jpg", "x4.jpg", "x10.jpg", "x11.jpg"}
        )]
        [InlineData(
            new[] {"x2.jpg", "x10.jpg", "x3.jpg", "x4.jpg", "x11.jpg"}, 
            new[] {"x2.jpg", "x3.jpg", "x4.jpg", "x10.jpg", "x11.jpg"}
        )]
        [InlineData(
            new[] {"Beelzebub_153b_RHS.zip", "Beelzebub_01_[Noodles].zip",}, 
            new[] {"Beelzebub_01_[Noodles].zip", "Beelzebub_153b_RHS.zip"}
        )]
        [InlineData(
            new[] {"[SCX-Scans]_Vandread_v02_Act02.zip", "[SCX-Scans]_Vandread_v02_Act01.zip","[SCX-Scans]_Vandread_v02_Act07.zip",}, 
            new[] {"[SCX-Scans]_Vandread_v02_Act01.zip", "[SCX-Scans]_Vandread_v02_Act02.zip","[SCX-Scans]_Vandread_v02_Act07.zip",}
        )]
        public void TestNaturalSortComparerLinq(string[] input, string[] expected)
        {
            NaturalSortComparer nc = new NaturalSortComparer();
            var output = input.OrderBy(c => c, nc);

            var i = 0;
            foreach (var s in output)
            {
                Assert.Equal(s, expected[i]);
                i++;
            }
        }
    }
}