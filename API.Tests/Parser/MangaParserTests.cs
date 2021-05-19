using System.Collections.Generic;
using API.Entities.Enums;
using API.Parser;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Parser
{
    public class MangaParserTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        
        public MangaParserTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

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
        [InlineData("Vol 1", "1")]
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
        [InlineData("Ichinensei_ni_Nacchattara_v02_ch11_[Taruby]_v1.3.zip", "2")]
        [InlineData("Dorohedoro v01 (2010) (Digital) (LostNerevarine-Empire).cbz", "1")]
        [InlineData("Dorohedoro v11 (2013) (Digital) (LostNerevarine-Empire).cbz", "11")]
        [InlineData("Dorohedoro v12 (2013) (Digital) (LostNerevarine-Empire).cbz", "12")]
        [InlineData("Yumekui_Merry_v01_c01[Bakayarou-Kuu].rar", "1")]
        [InlineData("Yumekui-Merry_DKThias_Chapter11v2.zip", "0")]
        [InlineData("Itoshi no Karin - c001-006x1 (v01) [Renzokusei Scans]", "1")]
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 12", "0")]
        [InlineData("VanDread-v01-c001[MD].zip", "1")]
        [InlineData("Ichiban_Ushiro_no_Daimaou_v04_ch27_[VISCANS].zip", "4")]
        [InlineData("Mob Psycho 100 v02 (2019) (Digital) (Shizu).cbz", "2")]
        [InlineData("Kodomo no Jikan vol. 1.cbz", "1")]
        [InlineData("Kodomo no Jikan vol. 10.cbz", "10")]
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 12 [Dametrans][v2]", "0")]
        [InlineData("Vagabond_v03", "3")]
        [InlineData("Mujaki No Rakune Volume 10.cbz", "10")]
        [InlineData("Umineko no Naku Koro ni - Episode 3 - Banquet of the Golden Witch #02.cbz", "0")]
        [InlineData("Volume 12 - Janken Boy is Coming!.cbz", "12")]
        [InlineData("[dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz", "20")]
        [InlineData("Gantz.V26.cbz", "26")]
        [InlineData("NEEDLESS_Vol.4_-Simeon_6_v2[SugoiSugoi].rar", "4")]
        [InlineData("[Hidoi]_Amaenaideyo_MS_vol01_chp02.rar", "1")]
        [InlineData("NEEDLESS_Vol.4_-_Simeon_6_v2_[SugoiSugoi].rar", "4")]
        [InlineData("Okusama wa Shougakusei c003 (v01) [bokuwaNEET]", "1")]
        [InlineData("Sword Art Online Vol 10 - Alicization Running [Yen Press] [LuCaZ] {r2}.epub", "10")]
        [InlineData("Noblesse - Episode 406 (52 Pages).7z", "0")]
        [InlineData("X-Men v1 #201 (September 2007).cbz", "1")]
        public void ParseVolumeTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseVolume(filename));
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
        [InlineData("U12 (Under 12) Vol. 0001 Ch. 0001 - Reiwa Scans (gb)", "U12")]
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
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 01", "Kedouin Makoto - Corpse Party Musume")]
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
        [InlineData("Vol 1.cbz", "")]
        [InlineData("Ichinensei_ni_Nacchattara_v01_ch01_[Taruby]_v1.1.zip", "Ichinensei ni Nacchattara")]
        [InlineData("Chrno_Crusade_Dragon_Age_All_Stars[AS].zip", "")]
        [InlineData("Ichiban_Ushiro_no_Daimaou_v04_ch34_[VISCANS].zip", "Ichiban Ushiro no Daimaou")]
        [InlineData("Rent a Girlfriend v01.cbr", "Rent a Girlfriend")]
        [InlineData("Yumekui_Merry_v01_c01[Bakayarou-Kuu].rar", "Yumekui Merry")]
        [InlineData("Itoshi no Karin - c001-006x1 (v01) [Renzokusei Scans]", "Itoshi no Karin")]
        [InlineData("Tonikaku Kawaii Vol-1 (Ch 01-08)", "Tonikaku Kawaii")]
        [InlineData("Tonikaku Kawaii (Ch 59-67) (Ongoing)", "Tonikaku Kawaii")]
        [InlineData("7thGARDEN v01 (2016) (Digital) (danke).cbz", "7thGARDEN")]
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 12", "Kedouin Makoto - Corpse Party Musume")]
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 09", "Kedouin Makoto - Corpse Party Musume")]
        [InlineData("Goblin Slayer Side Story - Year One 025.5", "Goblin Slayer Side Story - Year One")]
        [InlineData("Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire)", "Goblin Slayer - Brand New Day")]
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 01 [Dametrans][v2]", "Kedouin Makoto - Corpse Party Musume")]
        [InlineData("Vagabond_v03", "Vagabond")]
        [InlineData("[AN] Mahoutsukai to Deshi no Futekisetsu na Kankei Chp. 1", "Mahoutsukai to Deshi no Futekisetsu na Kankei")]
        [InlineData("Beelzebub_Side_Story_02_RHS.zip", "Beelzebub Side Story")]
        [InlineData("[BAA]_Darker_than_Black_Omake-1.zip", "Darker than Black")]
        [InlineData("Baketeriya ch01-05.zip", "Baketeriya")]
        [InlineData("[PROzess]Kimi_ha_midara_na_Boku_no_Joou_-_Ch01", "Kimi ha midara na Boku no Joou")]
        [InlineData("[SugoiSugoi]_NEEDLESS_Vol.2_-_Disk_The_Informant_5_[ENG].rar", "NEEDLESS")]
        [InlineData("Fullmetal Alchemist chapters 101-108.cbz", "Fullmetal Alchemist")]
        [InlineData("To Love Ru v09 Uncensored (Ch.071-079).cbz", "To Love Ru")]
        [InlineData("[dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz", "One Piece - Digital Colored Comics")]
        //[InlineData("Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U Extra Chapter", "Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U")]
        [InlineData("Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U Chapter 01", "Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U")]
        [InlineData("Vol03_ch15-22.rar", "")]
        [InlineData("Love Hina - Special.cbz", "")] // This has to be a fallback case
        [InlineData("Ani-Hina Art Collection.cbz", "")] // This has to be a fallback case
        [InlineData("Magi - Ch.252-005.cbz", "Magi")]
        [InlineData("Umineko no Naku Koro ni - Episode 1 - Legend of the Golden Witch #1", "Umineko no Naku Koro ni")]
        [InlineData("Kimetsu no Yaiba - Digital Colored Comics c162 Three Victorious Stars.cbz", "Kimetsu no Yaiba - Digital Colored Comics")]
        [InlineData("[Hidoi]_Amaenaideyo_MS_vol01_chp02.rar", "Amaenaideyo MS")]
        [InlineData("NEEDLESS_Vol.4_-_Simeon_6_v2_[SugoiSugoi].rar", "NEEDLESS")]
        [InlineData("Okusama wa Shougakusei c003 (v01) [bokuwaNEET]", "Okusama wa Shougakusei")]
        [InlineData("VanDread-v01-c001[MD].zip", "VanDread")]
        [InlineData("Momo The Blood Taker - Chapter 027 Violent Emotion.cbz", "Momo The Blood Taker")]
        [InlineData("Kiss x Sis - Ch.15 - The Angst of a 15 Year Old Boy.cbz", "Kiss x Sis")]
        [InlineData("Green Worldz - Chapter 112 Final Chapter (End).cbz", "Green Worldz")]
        [InlineData("Noblesse - Episode 406 (52 Pages).7z", "Noblesse")]
        [InlineData("X-Men v1 #201 (September 2007).cbz", "X-Men")]
        [InlineData("Kodoja #001 (March 2016)", "Kodoja")]
        [InlineData("Boku No Kokoro No Yabai Yatsu - Chapter 054 I Prayed At The Shrine (V0).cbz", "Boku No Kokoro No Yabai Yatsu")]
        public void ParseSeriesTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseSeries(filename));
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
        [InlineData("Beelzebub_01_[Noodles].zip", "1")]
        [InlineData("Yumekui-Merry_DKThias_Chapter21.zip", "21")]
        [InlineData("Yumekui_Merry_v01_c01[Bakayarou-Kuu].rar", "1")]
        [InlineData("Yumekui-Merry_DKThias_Chapter11v2.zip", "11")]
        [InlineData("Yumekui-Merry DKThiasScanlations Chapter51v2", "51")]
        [InlineData("Yumekui-Merry_DKThiasScanlations&RenzokuseiScans_Chapter61", "61")]
        [InlineData("Goblin Slayer Side Story - Year One 017.5", "17.5")]
        [InlineData("Beelzebub_53[KSH].zip", "53")]
        [InlineData("Black Bullet - v4 c20.5 [batoto]", "20.5")]
        [InlineData("Itoshi no Karin - c001-006x1 (v01) [Renzokusei Scans]", "1-6")]
        [InlineData("APOSIMZ 040 (2020) (Digital) (danke-Empire).cbz", "40")]
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 12", "12")]
        [InlineData("Vol 1", "0")]
        [InlineData("VanDread-v01-c001[MD].zip", "1")]
        [InlineData("Goblin Slayer Side Story - Year One 025.5", "25.5")]
        [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 01", "1")]
        [InlineData("To Love Ru v11 Uncensored (Ch.089-097+Omake)", "89-97")]
        [InlineData("To Love Ru v18 Uncensored (Ch.153-162.5)", "153-162.5")]
        [InlineData("[AN] Mahoutsukai to Deshi no Futekisetsu na Kankei Chp. 1", "1")]
        [InlineData("Beelzebub_Side_Story_02_RHS.zip", "2")]
        [InlineData("[PROzess]Kimi_ha_midara_na_Boku_no_Joou_-_Ch01", "1")]
        [InlineData("Fullmetal Alchemist chapters 101-108.cbz", "101-108")]
        [InlineData("Umineko no Naku Koro ni - Episode 3 - Banquet of the Golden Witch #02.cbz", "2")]
        [InlineData("To Love Ru v09 Uncensored (Ch.071-079).cbz", "71-79")]
        [InlineData("Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U Extra Chapter.rar", "0")]
        [InlineData("Beelzebub_153b_RHS.zip", "153.5")]
        [InlineData("Beelzebub_150-153b_RHS.zip", "150-153.5")]
        [InlineData("Transferred to another world magical swordsman v1.1", "1")]
        [InlineData("Transferred to another world magical swordsman v1.2", "2")]
        [InlineData("Kiss x Sis - Ch.15 - The Angst of a 15 Year Old Boy.cbz", "15")]
        [InlineData("Kiss x Sis - Ch.12 - 1 , 2 , 3P!.cbz", "12")]
        [InlineData("Umineko no Naku Koro ni - Episode 1 - Legend of the Golden Witch #1", "1")]
        [InlineData("Kiss x Sis - Ch.00 - Let's Start from 0.cbz", "0")]
        [InlineData("[Hidoi]_Amaenaideyo_MS_vol01_chp02.rar", "2")]
        [InlineData("Okusama wa Shougakusei c003 (v01) [bokuwaNEET]", "3")]
        [InlineData("Kiss x Sis - Ch.15 - The Angst of a 15 Year Old Boy.cbz", "15")]
        [InlineData("Tomogui Kyoushitsu - Chapter 006 Game 005 - Fingernails On Right Hand (Part 002).cbz", "6")]
        [InlineData("Noblesse - Episode 406 (52 Pages).7z", "406")]
        [InlineData("X-Men v1 #201 (September 2007).cbz", "201")]
        [InlineData("Kodoja #001 (March 2016)", "1")]
        [InlineData("Noblesse - Episode 429 (74 Pages).7z", "429")]
        [InlineData("Boku No Kokoro No Yabai Yatsu - Chapter 054 I Prayed At The Shrine (V0).cbz", "54")]
        public void ParseChaptersTest(string filename, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseChapter(filename));
        }


        [Theory]
        [InlineData("Tenjou Tenge Omnibus", "Omnibus")]
        [InlineData("Tenjou Tenge {Full Contact Edition}", "Full Contact Edition")]
        [InlineData("Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz", "Full Contact Edition")]
        [InlineData("Wotakoi - Love is Hard for Otaku Omnibus v01 (2018) (Digital) (danke-Empire)", "Omnibus")]
        [InlineData("To Love Ru v01 Uncensored (Ch.001-007)", "Uncensored")]
        [InlineData("Chobits Omnibus Edition v01 [Dark Horse]", "Omnibus Edition")]
        [InlineData("[dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz", "")]
        [InlineData("AKIRA - c003 (v01) [Full Color] [Darkhorse].cbz", "Full Color")]
        public void ParseEditionTest(string input, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseEdition(input));
        }
        [Theory]
        [InlineData("Beelzebub Special OneShot - Minna no Kochikame x Beelzebub (2016) [Mangastream].cbz", true)]
        [InlineData("Beelzebub_Omake_June_2012_RHS", true)]
        [InlineData("Beelzebub_Side_Story_02_RHS.zip", false)]
        [InlineData("Darker than Black Shikkoku no Hana Special [Simple Scans].zip", true)]
        [InlineData("Darker than Black Shikkoku no Hana Fanbook Extra [Simple Scans].zip", true)]
        [InlineData("Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U Extra Chapter", true)]
        [InlineData("Ani-Hina Art Collection.cbz", true)]
        [InlineData("Gifting The Wonderful World With Blessings! - 3 Side Stories [yuNS][Unknown]", true)]
        [InlineData("A Town Where You Live - Bonus Chapter.zip", true)]
        [InlineData("Yuki Merry - 4-Komga Anthology", true)]
        public void ParseMangaSpecialTest(string input, bool expected)
        {
            Assert.Equal(expected,  !string.IsNullOrEmpty(API.Parser.Parser.ParseMangaSpecial(input)));
        }
        
        [Theory]
        [InlineData("image.png", MangaFormat.Image)]
        [InlineData("image.cbz", MangaFormat.Archive)]
        [InlineData("image.txt", MangaFormat.Unknown)]
        public void ParseFormatTest(string inputFile, MangaFormat expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseFormat(inputFile));
        }

        [Theory]
        [InlineData("Gifting The Wonderful World With Blessings! - 3 Side Stories [yuNS][Unknown].epub", "Side Stories")]
        public void ParseSpecialTest(string inputFile, string expected)
        {
            Assert.Equal(expected, API.Parser.Parser.ParseMangaSpecial(inputFile));
        }

        [Fact]
        public void ParseInfoTest()
        {
            const string rootPath = @"E:/Manga/";
            var expected = new Dictionary<string, ParserInfo>();
            var filepath = @"E:/Manga/Mujaki no Rakuen/Mujaki no Rakuen Vol12 ch76.cbz";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Mujaki no Rakuen", Volumes = "12",
                Chapters = "76", Filename = "Mujaki no Rakuen Vol12 ch76.cbz", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });
            
            filepath = @"E:/Manga/Shimoneta to Iu Gainen ga Sonzai Shinai Taikutsu na Sekai Man-hen/Vol 1.cbz";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Shimoneta to Iu Gainen ga Sonzai Shinai Taikutsu na Sekai Man-hen", Volumes = "1",
                Chapters = "0", Filename = "Vol 1.cbz", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });
            
            filepath = @"E:\Manga\Beelzebub\Beelzebub_01_[Noodles].zip";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Beelzebub", Volumes = "0",
                Chapters = "1", Filename = "Beelzebub_01_[Noodles].zip", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });
            
            filepath = @"E:\Manga\Ichinensei ni Nacchattara\Ichinensei_ni_Nacchattara_v01_ch01_[Taruby]_v1.1.zip";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Ichinensei ni Nacchattara", Volumes = "1",
                Chapters = "1", Filename = "Ichinensei_ni_Nacchattara_v01_ch01_[Taruby]_v1.1.zip", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });

            filepath = @"E:\Manga\Tenjo Tenge (Color)\Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Tenjo Tenge", Volumes = "1", Edition = "Full Contact Edition",
                Chapters = "0", Filename = "Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz", Format = MangaFormat.Archive,
                FullFilePath = filepath
            }); 
            
            filepath = @"E:\Manga\Akame ga KILL! ZERO (2016-2019) (Digital) (LuCaZ)\Akame ga KILL! ZERO v01 (2016) (Digital) (LuCaZ).cbz";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Akame ga KILL! ZERO", Volumes = "1", Edition = "",
                Chapters = "0", Filename = "Akame ga KILL! ZERO v01 (2016) (Digital) (LuCaZ).cbz", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });
            
            filepath = @"E:\Manga\Dorohedoro\Dorohedoro v01 (2010) (Digital) (LostNerevarine-Empire).cbz";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Dorohedoro", Volumes = "1", Edition = "",
                Chapters = "0", Filename = "Dorohedoro v01 (2010) (Digital) (LostNerevarine-Empire).cbz", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });
            
            filepath = @"E:\Manga\APOSIMZ\APOSIMZ 040 (2020) (Digital) (danke-Empire).cbz";
            expected.Add(filepath, new ParserInfo
            {
                Series = "APOSIMZ", Volumes = "0", Edition = "",
                Chapters = "40", Filename = "APOSIMZ 040 (2020) (Digital) (danke-Empire).cbz", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });
            
            filepath = @"E:\Manga\Corpse Party Musume\Kedouin Makoto - Corpse Party Musume, Chapter 09.cbz";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Kedouin Makoto - Corpse Party Musume", Volumes = "0", Edition = "",
                Chapters = "9", Filename = "Kedouin Makoto - Corpse Party Musume, Chapter 09.cbz", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });
            
            filepath = @"E:\Manga\Goblin Slayer\Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire).cbz";
            expected.Add(filepath, new ParserInfo
            {
                Series = "Goblin Slayer - Brand New Day", Volumes = "0", Edition = "",
                Chapters = "6.5", Filename = "Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire).cbz", Format = MangaFormat.Archive,
                FullFilePath = filepath
            });


            foreach (var file in expected.Keys)
            {
                var expectedInfo = expected[file];
                var actual = API.Parser.Parser.Parse(file, rootPath);
                if (expectedInfo == null)
                {
                    Assert.Null(actual);
                    return;
                }
                Assert.NotNull(actual);
                _testOutputHelper.WriteLine($"Validating {file}");
                _testOutputHelper.WriteLine("Format");
                Assert.Equal(expectedInfo.Format, actual.Format);
                _testOutputHelper.WriteLine("Series");
                Assert.Equal(expectedInfo.Series, actual.Series);
                _testOutputHelper.WriteLine("Chapters");
                Assert.Equal(expectedInfo.Chapters, actual.Chapters);
                _testOutputHelper.WriteLine("Volumes");
                Assert.Equal(expectedInfo.Volumes, actual.Volumes);
                _testOutputHelper.WriteLine("Edition");
                Assert.Equal(expectedInfo.Edition, actual.Edition);
                _testOutputHelper.WriteLine("Filename");
                Assert.Equal(expectedInfo.Filename, actual.Filename);
                _testOutputHelper.WriteLine("FullFilePath");
                Assert.Equal(expectedInfo.FullFilePath, actual.FullFilePath);
            }
        }
    }
}