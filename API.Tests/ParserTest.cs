using Xunit;
using static API.Parser.Parser;

namespace API.Tests
{
    public class ParserTests
    {
        [Theory]
        [InlineData("Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)", "1")]
        [InlineData("My Girlfriend Is Shobitch v01 - ch. 09 - pg. 008.png", "1")]
        [InlineData("Historys Strongest Disciple Kenichi_v11_c90-98.zip", "11")]
        [InlineData("B_Gata_H_Kei_v01[SlowManga&OverloadScans]", "1")]
        [InlineData("BTOOOM! v01 (2013) (Digital) (Shadowcat-Empire)", "1")]
        [InlineData("Gokukoku no Brynhildr - c001-008 (v01) [TrinityBAKumA]", "1")]
        //[InlineData("Dance in the Vampire Bund v16-17 (Digital) (NiceDragon)", "16-17")]
        [InlineData("Akame ga KILL! ZERO v01 (2016) (Digital) (LuCaZ).cbz", "1")]
        [InlineData("v001", "1")]
        [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip", "1")]
        public void ParseVolumeTest(string filename, string expected)
        {
            var result = ParseVolume(filename);
            Assert.Equal(expected, result);
        }
        
        [Theory]
        [InlineData("Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)", "Killing Bites")]
        [InlineData("My Girlfriend Is Shobitch v01 - ch. 09 - pg. 008.png", "My Girlfriend Is Shobitch")]
        [InlineData("Historys Strongest Disciple Kenichi_v11_c90-98.zip", "Historys Strongest Disciple Kenichi")]
        [InlineData("B_Gata_H_Kei_v01[SlowManga&OverloadScans]", "B Gata H Kei")]
        [InlineData("BTOOOM! v01 (2013) (Digital) (Shadowcat-Empire)", "BTOOOM!")]
        [InlineData("Gokukoku no Brynhildr - c001-008 (v01) [TrinityBAKumA]", "Gokukoku no Brynhildr")]
        [InlineData("Dance in the Vampire Bund v16-17 (Digital) (NiceDragon)", "Dance in the Vampire Bund")]
        [InlineData("v001", "")]
        [InlineData("Akame ga KILL! ZERO (2016-2019) (Digital) (LuCaZ)", "Akame ga KILL! ZERO")]
        public void ParseSeriesTest(string filename, string expected)
        {
            var result = ParseSeries(filename);
            Assert.Equal(expected, result);
        }
        
        [Theory]
        [InlineData("Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)", "1")]
        [InlineData("My Girlfriend Is Shobitch v01 - ch. 09 - pg. 008.png", "9")]
        [InlineData("Historys Strongest Disciple Kenichi_v11_c90-98.zip", "90-98")]
        [InlineData("B_Gata_H_Kei_v01[SlowManga&OverloadScans]", "")]
        [InlineData("BTOOOM! v01 (2013) (Digital) (Shadowcat-Empire)", "")]
        [InlineData("Gokukoku no Brynhildr - c001-008 (v01) [TrinityBAKumA]", "1-8")]
        [InlineData("Dance in the Vampire Bund v16-17 (Digital) (NiceDragon)", "")]
        [InlineData("c001", "1")]
        [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.12.zip", "12")]
        public void ParseChaptersTest(string filename, string expected)
        {
            var result = ParseChapter(filename);
            Assert.Equal(expected, result);
        }
 

        [Theory]
        [InlineData("0001", "1")]
        [InlineData("1", "1")]
        [InlineData("0013", "13")]
        public void RemoveLeadingZeroesTest(string input, string expected)
        {
            Assert.Equal(expected, RemoveLeadingZeroes(input));
        }

        [Theory]
        [InlineData("1", "001")]
        [InlineData("10", "010")]
        [InlineData("100", "100")]
        public void PadZerosTest(string input, string expected)
        {
            Assert.Equal(expected, PadZeros(input));
        }

        [Theory]
        [InlineData("Hello_I_am_here", "Hello I am here")]
        [InlineData("Hello_I_am_here   ", "Hello I am here")]
        [InlineData("[ReleaseGroup] The Title", "The Title")]
        [InlineData("[ReleaseGroup]_The_Title", "The Title")]
        public void CleanTitleTest(string input, string expected)
        {
            Assert.Equal(expected, CleanTitle(input));
        }
        
        [Theory]
        [InlineData("test.cbz", true)]
        [InlineData("test.cbr", true)]
        [InlineData("test.zip", true)]
        [InlineData("test.rar", true)]
        public void IsArchiveTest(string input, bool expected)
        {
            Assert.Equal(expected, IsArchive(input));
        }
    }
}