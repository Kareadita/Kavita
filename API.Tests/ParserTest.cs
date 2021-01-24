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
        [InlineData("Dance in the Vampire Bund v16-17 (Digital) (NiceDragon)", "16-17")]
        [InlineData("Akame ga KILL! ZERO v01 (2016) (Digital) (LuCaZ).cbz", "1")]
        [InlineData("v001", "1")]
        [InlineData("No Volume", "0")]
        [InlineData("U12 (Under 12) Vol. 0001 Ch. 0001 - Reiwa Scans (gb)", "1")]
        [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip", "1")]
        [InlineData("Tonikaku Cawaii [Volume 11].cbz", "11")]
        [InlineData("[WS]_Ichiban_Ushiro_no_Daimaou_v02_ch10.zip", "2")]
        [InlineData("[xPearse] Kyochuu Rettou Volume 1 [English] [Manga] [Volume Scans]", "1")]
        [InlineData("Tower Of God S01 014 (CBT) (digital).cbz", "1")]
        [InlineData("Tenjou_Tenge_v17_c100[MT].zip", "17")]
        [InlineData("Shimoneta - Manmaru Hen - c001-006 (v01) [Various].zip", "1")]
        [InlineData("Future Diary v02 (2009) (Digital) (Viz).cbz", "2")]
        [InlineData("Mujaki no Rakuen Vol12 ch76", "12")]
        public void ParseVolumeTest(string filename, string expected)
        {
            Assert.Equal(expected, ParseVolume(filename));
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
        [InlineData("U12 (Under 12) Vol. 0001 Ch. 0001 - Reiwa Scans (gb)", "U12 (Under 12)")]
        [InlineData("Akame ga KILL! ZERO (2016-2019) (Digital) (LuCaZ)", "Akame ga KILL! ZERO")]
        [InlineData("APOSIMZ 017 (2018) (Digital) (danke-Empire).cbz", "APOSIMZ")]
        [InlineData("Akiiro Bousou Biyori - 01.jpg", "Akiiro Bousou Biyori")]
        [InlineData("Beelzebub_172_RHS.zip", "Beelzebub")]
        [InlineData("Dr. STONE 136 (2020) (Digital) (LuCaZ).cbz", "Dr. STONE")]
        [InlineData("Cynthia the Mission 29.rar", "Cynthia the Mission")]
        [InlineData("Darling in the FranXX - Volume 01.cbz", "Darling in the FranXX")]
        [InlineData("Darwin's Game - Volume 14 (F).cbz", "Darwin's Game")]
        [InlineData("[BAA]_Darker_than_Black_c7.zip", "Darker than Black")]
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 19 [Dametrans].zip", "Kedouin Makoto - Corpse Party Musume")]
        [InlineData("[WS]_Ichiban_Ushiro_no_Daimaou_v02_ch10.zip", "Ichiban Ushiro no Daimaou")]
        [InlineData("[xPearse] Kyochuu Rettou Volume 1 [English] [Manga] [Volume Scans]", "Kyochuu Rettou")]
        [InlineData("Loose_Relation_Between_Wizard_and_Apprentice_c07[AN].zip", "Loose Relation Between Wizard and Apprentice")]
        [InlineData("Tower Of God S01 014 (CBT) (digital).cbz", "Tower Of God")]
        [InlineData("Tenjou_Tenge_c106[MT].zip", "Tenjou Tenge")]
        [InlineData("Tenjou_Tenge_v17_c100[MT].zip", "Tenjou Tenge")]
        [InlineData("Shimoneta - Manmaru Hen - c001-006 (v01) [Various].zip", "Shimoneta - Manmaru Hen")]
        [InlineData("Future Diary v02 (2009) (Digital) (Viz).cbz", "Future Diary")]
        [InlineData("Tonikaku Cawaii [Volume 11].cbz", "Tonikaku Cawaii")]
        [InlineData("Mujaki no Rakuen Vol12 ch76", "Mujaki no Rakuen")]
        [InlineData("Knights of Sidonia c000 (S2 LE BD Omake - BLAME!) [Habanero Scans]", "Knights of Sidonia")]
        public void ParseSeriesTest(string filename, string expected)
        {
            Assert.Equal(expected, ParseSeries(filename));
        }

        [Theory]
        [InlineData("Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)", "1")]
        [InlineData("My Girlfriend Is Shobitch v01 - ch. 09 - pg. 008.png", "9")]
        [InlineData("Historys Strongest Disciple Kenichi_v11_c90-98.zip", "90-98")]
        [InlineData("B_Gata_H_Kei_v01[SlowManga&OverloadScans]", "0")]
        [InlineData("BTOOOM! v01 (2013) (Digital) (Shadowcat-Empire)", "0")]
        [InlineData("Gokukoku no Brynhildr - c001-008 (v01) [TrinityBAKumA]", "1-8")]
        [InlineData("Dance in the Vampire Bund v16-17 (Digital) (NiceDragon)", "0")]
        [InlineData("c001", "1")]
        [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.12.zip", "12")]
        [InlineData("Adding volume 1 with File: Ana Satsujin Vol. 1 Ch. 5 - Manga Box (gb).cbz", "5")]
        [InlineData("Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz", "18")]
        [InlineData("Cynthia The Mission - c000-006 (v06) [Desudesu&Brolen].zip", "0-6")]
        [InlineData("[WS]_Ichiban_Ushiro_no_Daimaou_v02_ch10.zip", "10")]
        [InlineData("Loose_Relation_Between_Wizard_and_Apprentice_c07[AN].zip", "7")]
        [InlineData("Tower Of God S01 014 (CBT) (digital).cbz", "14")]
        [InlineData("Tenjou_Tenge_c106[MT].zip", "106")]
        [InlineData("Tenjou_Tenge_v17_c100[MT].zip", "100")]
        [InlineData("Shimoneta - Manmaru Hen - c001-006 (v01) [Various].zip", "1-6")]
        [InlineData("Mujaki no Rakuen Vol12 ch76", "76")]
        public void ParseChaptersTest(string filename, string expected)
        {
            Assert.Equal(expected, ParseChapter(filename));
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
        [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1", "Kasumi Otoko no Ko v1.1")]
        public void CleanTitleTest(string input, string expected)
        {
            Assert.Equal(expected, CleanTitle(input));
        }
        
        [Theory]
        [InlineData("test.cbz", true)]
        [InlineData("test.cbr", false)]
        [InlineData("test.zip", true)]
        [InlineData("test.rar", false)]
        [InlineData("test.rar.!qb", false)]
        [InlineData("[shf-ma-khs-aqs]negi_pa_vol15007.jpg", false)]
        public void IsArchiveTest(string input, bool expected)
        {
            Assert.Equal(expected, IsArchive(input));
        }

        [Theory]
        [InlineData("Tenjou Tenge Omnibus", "Omnibus")]
        [InlineData("Tenjou Tenge {Full Contact Edition}", "Full Contact Edition")]
        [InlineData("Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz", "Full Contact Edition")]
        public void ParseEditionTest(string input, string expected)
        {
            Assert.Equal(expected, ParseEdition(input));
        }
    }
}