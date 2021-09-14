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
        public void ParseComicVolumeTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseComicVolume(filename));
        }

        [Theory]
        [InlineData("01 Spider-Man & Wolverine 01.cbr", "0")]
        [InlineData("04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)", "0")]
        [InlineData("The First Asterix Frieze (WebP by Doc MaKS)", "0")]
        [InlineData("Batman & Catwoman - Trail of the Gun 01", "0")]
        [InlineData("Batman & Daredevil - King of New York", "0")]
        [InlineData("Batman & Grendel (1996) 01 - Devil's Bones", "1")]
        [InlineData("Batman & Robin the Teen Wonder #0", "0")]
        [InlineData("Batman & Wildcat (1 of 3)", "1")]
        [InlineData("Batman & Wildcat (2 of 3)", "2")]
        [InlineData("Batman And Superman World's Finest #01", "0")]
        [InlineData("Babe 01", "0")]
        [InlineData("Scott Pilgrim 01 - Scott Pilgrim's Precious Little Life (2004)", "1")]
        [InlineData("Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)", "1")]
        [InlineData("Superman v1 024 (09-10 1943)", "24")]
        [InlineData("Invincible 070.5 - Invincible Returns 1 (2010) (digital) (Minutemen-InnerDemons).cbr", "70.5")]
        [InlineData("Amazing Man Comics chapter 25", "25")]
        [InlineData("Invincible 033.5 - Marvel Team-Up 14 (2006) (digital) (Minutemen-Slayer)", "33.5")]
        [InlineData("Batman Wayne Family Adventures - Ep. 014 - Moving In", "14")]
        public void ParseComicChapterTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseComicChapter(filename));
        }
    }
}
