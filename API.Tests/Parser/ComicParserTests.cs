using Xunit;

namespace API.Tests.Parser
{
    public class ComicParserTests
    {
        [Theory]
        [InlineData("01 Spider-Man & Wolverine 01.cbr", "Spider-Man & Wolverine")]
        [InlineData("04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)", "Asterix the Gladiator")]
        [InlineData("The First Asterix Frieze (WebP by Doc MaKS)", "The First Asterix Frieze")]
        [InlineData("Batman & Catwoman - Trail of the Gun 01", "Batman & Catwoman - Trail of the Gun")]
        [InlineData("Batman & Daredevil - King of New York", "Batman & Daredevil - King of New York")]
        [InlineData("Batman & Grendel (1996) 01 - Devil's Bones", "Batman & Grendel")]
        [InlineData("Batman & Robin the Teen Wonder #0", "Batman & Robin the Teen Wonder")]
        [InlineData("Batman & Wildcat (1 of 3)", "Batman & Wildcat")]
        [InlineData("Batman And Superman World's Finest #01", "Batman And Superman World's Finest")]
        [InlineData("Babe 01", "Babe")]
        [InlineData("Scott Pilgrim 01 - Scott Pilgrim's Precious Little Life (2004)", "Scott Pilgrim")]
        [InlineData("Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)", "Teen Titans")]
        [InlineData("Scott Pilgrim 02 - Scott Pilgrim vs. The World (2005)", "Scott Pilgrim")]
        [InlineData("Wolverine - Origins 003 (2006) (digital) (Minutemen-PhD)", "Wolverine - Origins")]
        [InlineData("Invincible Vol 01 Family matters (2005) (Digital).cbr", "Invincible")]
        [InlineData("Amazing Man Comics chapter 25", "Amazing Man Comics")]
        [InlineData("Amazing Man Comics issue #25", "Amazing Man Comics")]
        [InlineData("Teen Titans v1 038 (1972) (c2c).cbr", "Teen Titans")]
        [InlineData("Batman Beyond 02 (of 6) (1999)", "Batman Beyond")]
        [InlineData("Batman Beyond - Return of the Joker (2001)", "Batman Beyond - Return of the Joker")]
        [InlineData("Invincible 033.5 - Marvel Team-Up 14 (2006) (digital) (Minutemen-Slayer)", "Invincible")]
        [InlineData("Batman Wayne Family Adventures - Ep. 001 - Moving In", "Batman Wayne Family Adventures")]
        [InlineData("Saga 001 (2012) (Digital) (Empire-Zone).cbr", "Saga")]
        [InlineData("spawn-123", "spawn")]
        [InlineData("Batman Beyond 04 (of 6) (1999)", "Batman Beyond")]
        [InlineData("Batman Beyond 001 (2012)", "Batman Beyond")]
        [InlineData("Batman Beyond 2.0 001 (2013)", "Batman Beyond 2.0")]
        [InlineData("Batman - Catwoman 001 (2021) (Webrip) (The Last Kryptonian-DCP)", "Batman - Catwoman")]
        [InlineData("Chew v1 - Taster´s Choise (2012) (Digital) (1920) (Kingpin-Empire)", "Chew")]
        [InlineData("Chew Script Book (2011) (digital-Empire) SP04", "Chew Script Book")]
        [InlineData("Batman - Detective Comics - Rebirth Deluxe Edition Book 02 (2018) (digital) (Son of Ultron-Empire)", "Batman - Detective Comics - Rebirth Deluxe Edition Book")]
        public void ParseComicSeriesTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseComicSeries(filename));
        }

        [Theory]
        [InlineData("01 Spider-Man & Wolverine 01.cbr", "0")]
        [InlineData("04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)", "0")]
        [InlineData("The First Asterix Frieze (WebP by Doc MaKS)", "0")]
        [InlineData("Batman & Catwoman - Trail of the Gun 01", "0")]
        [InlineData("Batman & Daredevil - King of New York", "0")]
        [InlineData("Batman & Grendel (1996) 01 - Devil's Bones", "0")]
        [InlineData("Batman & Robin the Teen Wonder #0", "0")]
        [InlineData("Batman & Wildcat (1 of 3)", "0")]
        [InlineData("Batman And Superman World's Finest #01", "0")]
        [InlineData("Babe 01", "0")]
        [InlineData("Scott Pilgrim 01 - Scott Pilgrim's Precious Little Life (2004)", "0")]
        [InlineData("Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)", "1")]
        [InlineData("Scott Pilgrim 02 - Scott Pilgrim vs. The World (2005)", "0")]
        [InlineData("Superman v1 024 (09-10 1943)", "1")]
        [InlineData("Amazing Man Comics chapter 25", "0")]
        [InlineData("Invincible 033.5 - Marvel Team-Up 14 (2006) (digital) (Minutemen-Slayer)", "0")]
        [InlineData("Cyberpunk 2077 - Trauma Team 04.cbz", "0")]
        [InlineData("spawn-123", "0")]
        [InlineData("Batman Beyond 04 (of 6) (1999)", "0")]
        [InlineData("Batman Beyond 001 (2012)", "0")]
        [InlineData("Batman Beyond 2.0 001 (2013)", "0")]
        [InlineData("Batman - Catwoman 001 (2021) (Webrip) (The Last Kryptonian-DCP)", "0")]
        [InlineData("Chew v1 - Taster´s Choise (2012) (Digital) (1920) (Kingpin-Empire)", "1")]
        [InlineData("Chew Script Book (2011) (digital-Empire) SP04", "0")]
        public void ParseComicVolumeTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseComicVolume(filename));
        }

        [Theory]
        [InlineData("01 Spider-Man & Wolverine 01.cbr", "1")]
        [InlineData("04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)", "0")]
        [InlineData("The First Asterix Frieze (WebP by Doc MaKS)", "0")]
        [InlineData("Batman & Catwoman - Trail of the Gun 01", "1")]
        [InlineData("Batman & Daredevil - King of New York", "0")]
        [InlineData("Batman & Grendel (1996) 01 - Devil's Bones", "1")]
        [InlineData("Batman & Robin the Teen Wonder #0", "0")]
        [InlineData("Batman & Wildcat (1 of 3)", "1")]
        [InlineData("Batman & Wildcat (2 of 3)", "2")]
        [InlineData("Batman And Superman World's Finest #01", "1")]
        [InlineData("Babe 01", "1")]
        [InlineData("Scott Pilgrim 01 - Scott Pilgrim's Precious Little Life (2004)", "1")]
        [InlineData("Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)", "1")]
        [InlineData("Superman v1 024 (09-10 1943)", "24")]
        [InlineData("Invincible 070.5 - Invincible Returns 1 (2010) (digital) (Minutemen-InnerDemons).cbr", "70.5")]
        [InlineData("Amazing Man Comics chapter 25", "25")]
        [InlineData("Invincible 033.5 - Marvel Team-Up 14 (2006) (digital) (Minutemen-Slayer)", "33.5")]
        [InlineData("Batman Wayne Family Adventures - Ep. 014 - Moving In", "14")]
        [InlineData("Saga 001 (2012) (Digital) (Empire-Zone)", "1")]
        [InlineData("spawn-123", "123")]
        [InlineData("Batman Beyond 04 (of 6) (1999)", "4")]
        [InlineData("Invincible 052 (c2c) (2008) (Minutemen-TheCouple)", "52")]
        [InlineData("Y - The Last Man #001", "1")]
        [InlineData("Batman Beyond 001 (2012)", "1")]
        [InlineData("Batman Beyond 2.0 001 (2013)", "1")]
        [InlineData("Batman - Catwoman 001 (2021) (Webrip) (The Last Kryptonian-DCP)", "1")]
        [InlineData("Chew v1 - Taster´s Choise (2012) (Digital) (1920) (Kingpin-Empire)", "0")]
        [InlineData("Chew Script Book (2011) (digital-Empire) SP04", "0")]
        public void ParseComicChapterTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseComicChapter(filename));
        }


        [Theory]
        [InlineData("Batman - Detective Comics - Rebirth Deluxe Edition Book 02 (2018) (digital) (Son of Ultron-Empire)", true)]
        public void ParseComcSpecialTest(string input, bool expected)
        {
            Assert.Equal(expected, !string.IsNullOrEmpty(API.Parser.Parser.ParseComicSpecial(input)));
        }

    }
}
