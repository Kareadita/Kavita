using System.Collections.Generic;
using System.Linq;
using API.Data.Misc;
using API.Entities.Enums;
using API.Extensions;
using Xunit;

namespace API.Tests.Extensions;

public class EnumerableExtensionsTests
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
        [InlineData(
            new[] {"Marvel2In1-7", "Marvel2In1-7-01", "Marvel2In1-7-02"},
            new[] {"Marvel2In1-7", "Marvel2In1-7-01", "Marvel2In1-7-02"}
        )]
        [InlineData(
            new[] {"001", "002", "!001"},
            new[] {"!001", "001", "002"}
        )]
        [InlineData(
            new[] {"001.jpg", "002.jpg", "!001.jpg"},
            new[] {"!001.jpg", "001.jpg", "002.jpg"}
        )]
        [InlineData(
            new[] {"001", "002", "!002"},
            new[] {"!002", "001", "002"}
        )]
        [InlineData(
            new[] {"001", ""},
            new[] {"", "001"}
        )]
        [InlineData(
            new[] {"Honzuki no Gekokujou_ Part 2/_Ch.019/002.jpg", "Honzuki no Gekokujou_ Part 2/_Ch.019/001.jpg", "Honzuki no Gekokujou_ Part 2/_Ch.020/001.jpg"},
            new[] {"Honzuki no Gekokujou_ Part 2/_Ch.019/001.jpg", "Honzuki no Gekokujou_ Part 2/_Ch.019/002.jpg", "Honzuki no Gekokujou_ Part 2/_Ch.020/001.jpg"}
        )]
        [InlineData(
            new[] {@"F:\/Anime_Series_Pelis/MANGA/Mangahere (EN)\Kirara Fantasia\_Ch.001\001.jpg", @"F:\/Anime_Series_Pelis/MANGA/Mangahere (EN)\Kirara Fantasia\_Ch.001\002.jpg"},
            new[] {@"F:\/Anime_Series_Pelis/MANGA/Mangahere (EN)\Kirara Fantasia\_Ch.001\001.jpg", @"F:\/Anime_Series_Pelis/MANGA/Mangahere (EN)\Kirara Fantasia\_Ch.001\002.jpg"}
        )]
        [InlineData(
            new[] {"01/001.jpg", "001.jpg"},
            new[] {"001.jpg", "01/001.jpg"}
        )]
        public void TestNaturalSort(string[] input, string[] expected)
        {
            Assert.Equal(expected, input.OrderByNatural(x => x).ToArray());
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
            new[] {"001", "002", "!001"},
            new[] {"!001", "001", "002"}
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
            new[] {"Honzuki no Gekokujou_ Part 2/_Ch.019/002.jpg", "Honzuki no Gekokujou_ Part 2/_Ch.019/001.jpg"},
            new[] {"Honzuki no Gekokujou_ Part 2/_Ch.019/001.jpg", "Honzuki no Gekokujou_ Part 2/_Ch.019/002.jpg"}
        )]
        public void TestNaturalSortLinq(string[] input, string[] expected)
        {
            var output = input.OrderByNatural(x => x);

            var i = 0;
            foreach (var s in output)
            {
                Assert.Equal(s, expected[i]);
                i++;
            }
        }

        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 1)]
        public void RestrictAgainstAgeRestriction_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
        {
            var items = new List<RecentlyAddedSeries>()
            {
                new RecentlyAddedSeries()
                {
                    AgeRating = AgeRating.Teen,
                },
                new RecentlyAddedSeries()
                {
                    AgeRating = AgeRating.Unknown,
                },
                new RecentlyAddedSeries()
                {
                    AgeRating = AgeRating.X18Plus,
                },
            };

            var filtered = items.RestrictAgainstAgeRestriction(new AgeRestriction()
            {
                AgeRating = AgeRating.Teen,
                IncludeUnknowns = includeUnknowns
            });
            Assert.Equal(expectedCount, filtered.Count());
        }
}
