﻿using System;
using System.Linq;
using API.Comparators;
using Xunit;

namespace API.Tests.Comparers
{
    public class NaturalSortComparerTest
    {
        private readonly NaturalSortComparer _nc = new NaturalSortComparer();
        
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
        [InlineData(
            new[] {"Frogman v01 001.jpg", "Frogman v01 ch01 p00 Credits.jpg",}, 
            new[] {"Frogman v01 001.jpg", "Frogman v01 ch01 p00 Credits.jpg",}
        )]
        [InlineData(
            new[] {"001.jpg", "10.jpg",}, 
            new[] {"001.jpg", "10.jpg",}
        )]
        [InlineData(
            new[] {"10/001.jpg", "10.jpg",}, 
            new[] {"10.jpg", "10/001.jpg",}
        )]
        [InlineData(
            new[] {"Batman - Black white vol 1 #04.cbr", "Batman - Black white vol 1 #03.cbr", "Batman - Black white vol 1 #01.cbr", "Batman - Black white vol 1 #02.cbr"}, 
            new[] {"Batman - Black white vol 1 #01.cbr", "Batman - Black white vol 1 #02.cbr", "Batman - Black white vol 1 #03.cbr", "Batman - Black white vol 1 #04.cbr"}
        )]
        [InlineData(
            new[] {"3and4.cbz", "The World God Only Knows - Oneshot.cbz", "5.cbz", "1and2.cbz"}, 
            new[] {"1and2.cbz", "3and4.cbz", "5.cbz", "The World God Only Knows - Oneshot.cbz"}
        )]
        [InlineData(
            new[] {"Solo Leveling - c000 (v01) - p000 [Cover] [dig] [Yen Press] [LuCaZ].jpg", "Solo Leveling - c000 (v01) - p001 [dig] [Yen Press] [LuCaZ].jpg", "Solo Leveling - c000 (v01) - p002 [dig] [Yen Press] [LuCaZ].jpg", "Solo Leveling - c000 (v01) - p003 [dig] [Yen Press] [LuCaZ].jpg"}, 
            new[] {"Solo Leveling - c000 (v01) - p000 [Cover] [dig] [Yen Press] [LuCaZ].jpg", "Solo Leveling - c000 (v01) - p001 [dig] [Yen Press] [LuCaZ].jpg", "Solo Leveling - c000 (v01) - p002 [dig] [Yen Press] [LuCaZ].jpg", "Solo Leveling - c000 (v01) - p003 [dig] [Yen Press] [LuCaZ].jpg"}
        )]
        public void TestNaturalSortComparer(string[] input, string[] expected)
        {
            Array.Sort(input, _nc);

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
        [InlineData(
            new[] {"Frogman v01 001.jpg", "Frogman v01 ch01 p00 Credits.jpg",}, 
            new[] {"Frogman v01 001.jpg", "Frogman v01 ch01 p00 Credits.jpg",}
        )]
        [InlineData(
            new[] {"001.jpg", "10.jpg",}, 
            new[] {"001.jpg", "10.jpg",}
        )]
        [InlineData(
            new[] {"10/001.jpg", "10.jpg",}, 
            new[] {"10.jpg", "10/001.jpg",}
        )]
        [InlineData(
            new[] {"Batman - Black white vol 1 #04.cbr", "Batman - Black white vol 1 #03.cbr", "Batman - Black white vol 1 #01.cbr", "Batman - Black white vol 1 #02.cbr"}, 
            new[] {"Batman - Black white vol 1 #01.cbr", "Batman - Black white vol 1 #02.cbr", "Batman - Black white vol 1 #03.cbr", "Batman - Black white vol 1 #04.cbr"}
        )]
        public void TestNaturalSortComparerLinq(string[] input, string[] expected)
        {
            var output = input.OrderBy(c => c, _nc);

            var i = 0;
            foreach (var s in output)
            {
                Assert.Equal(s, expected[i]);
                i++;
            }
        }
    }
}