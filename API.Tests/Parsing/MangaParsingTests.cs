using API.Entities.Enums;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Parsing;

public class MangaParsingTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public MangaParsingTests(ITestOutputHelper testOutputHelper)
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
    [InlineData("vol_356-1", "356")] // Mangapy syntax
    [InlineData("No Volume", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)]
    [InlineData("U12 (Under 12) Vol. 0001 Ch. 0001 - Reiwa Scans (gb)", "1")]
    [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip", "1.1")]
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
    [InlineData("Yumekui_Merry_v01_c01[Bakayarou-Kuu].rar", "1")]
    [InlineData("Yumekui-Merry_DKThias_Chapter11v2.zip", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)]
    [InlineData("Itoshi no Karin - c001-006x1 (v01) [Renzokusei Scans]", "1")]
    [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 12", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)]
    [InlineData("VanDread-v01-c001[MD].zip", "1")]
    [InlineData("Ichiban_Ushiro_no_Daimaou_v04_ch27_[VISCANS].zip", "4")]
    [InlineData("Mob Psycho 100 v02 (2019) (Digital) (Shizu).cbz", "2")]
    [InlineData("Kodomo no Jikan vol. 1.cbz", "1")]
    [InlineData("Kodomo no Jikan vol. 10.cbz", "10")]
    [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 12 [Dametrans][v2]", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)]
    [InlineData("Vagabond_v03", "3")]
    [InlineData("Mujaki No Rakune Volume 10.cbz", "10")]
    [InlineData("Umineko no Naku Koro ni - Episode 3 - Banquet of the Golden Witch #02.cbz", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)]
    [InlineData("Volume 12 - Janken Boy is Coming!.cbz", "12")]
    [InlineData("[dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz", "20")]
    [InlineData("Gantz.V26.cbz", "26")]
    [InlineData("NEEDLESS_Vol.4_-Simeon_6_v2[SugoiSugoi].rar", "4")]
    [InlineData("[Hidoi]_Amaenaideyo_MS_vol01_chp02.rar", "1")]
    [InlineData("NEEDLESS_Vol.4_-_Simeon_6_v2_[SugoiSugoi].rar", "4")]
    [InlineData("Okusama wa Shougakusei c003 (v01) [bokuwaNEET]", "1")]
    [InlineData("Sword Art Online Vol 10 - Alicization Running [Yen Press] [LuCaZ] {r2}.epub", "10")]
    [InlineData("Noblesse - Episode 406 (52 Pages).7z", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)]
    [InlineData("X-Men v1 #201 (September 2007).cbz", "1")]
    [InlineData("Hentai Ouji to Warawanai Neko. - Vol. 06 Ch. 034.5", "6")]
    [InlineData("The 100 Girlfriends Who Really, Really, Really, Really, Really Love You - Vol. 03 Ch. 023.5 - Volume 3 Extras.cbz", "3")]
    [InlineData("The 100 Girlfriends Who Really, Really, Really, Really, Really Love You - Vol. 03.5 Ch. 023.5 - Volume 3 Extras.cbz", "3.5")]
    [InlineData("幽游白书完全版 第03卷 天下", "3")]
    [InlineData("阿衰online 第1册", "1")]
    [InlineData("【TFO汉化&Petit汉化】迷你偶像漫画卷2第25话", "2")]
    [InlineData("スライム倒して300年、知らないうちにレベルMAXになってました 1巻", "1")]
    [InlineData("スライム倒して300年、知らないうちにレベルMAXになってました 1-3巻", "1-3")]
    [InlineData("Dance in the Vampire Bund {Special Edition} v03.5 (2019) (Digital) (KG Manga)", "3.5")]
    [InlineData("Kebab Том 1 Глава 3", "1")]
    [InlineData("Манга Глава 2", API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)]
    [InlineData("Манга Тома 1-4", "1-4")]
    [InlineData("Манга Том 1-4", "1-4")]
    [InlineData("조선왕조실톡 106화", "106")]
    [InlineData("죽음 13회", "13")]
    [InlineData("동의보감 13장", "13")]
    [InlineData("몰?루 아카이브 7.5권", "7.5")]
    [InlineData("63권#200", "63")]
    [InlineData("시즌34삽화2", "34")]
    [InlineData("Accel World Chapter 001 Volume 002", "2")]
    [InlineData("Accel World Volume 2", "2")]
    [InlineData("Nagasarete Airantou - Vol. 30 Ch. 187.5 - Vol.31 Omake", "30")]
    public void ParseVolumeTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseVolume(filename, LibraryType.Manga));
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
    [InlineData("Kiss x Sis - Ch.36 - A Cold Home Visit.cbz", "Kiss x Sis")]
    [InlineData("Seraph of the End - Vampire Reign 093 (2020) (Digital) (LuCaZ)", "Seraph of the End - Vampire Reign")]
    [InlineData("Grand Blue Dreaming - SP02 Extra (2019) (Digital) (danke-Empire).cbz", "Grand Blue Dreaming")]
    [InlineData("Yuusha Ga Shinda! - Vol.tbd Chapter 27.001 V2 Infection ①.cbz", "Yuusha Ga Shinda!")]
    [InlineData("Seraph of the End - Vampire Reign 093 (2020) (Digital) (LuCaZ).cbz", "Seraph of the End - Vampire Reign")]
    [InlineData("Getsuyoubi no Tawawa - Ch. 001 - Ai-chan, Part 1", "Getsuyoubi no Tawawa")]
    [InlineData("Please Go Home, Akutsu-San! - Chapter 038.5 - Volume Announcement.cbz", "Please Go Home, Akutsu-San!")]
    [InlineData("Killing Bites - Vol 11 Chapter 050 Save Me, Nunupi!.cbz", "Killing Bites")]
    [InlineData("Mad Chimera World - Volume 005 - Chapter 026.cbz", "Mad Chimera World")]
    [InlineData("Hentai Ouji to Warawanai Neko. - Vol. 06 Ch. 034.5", "Hentai Ouji to Warawanai Neko.")]
    [InlineData("The 100 Girlfriends Who Really, Really, Really, Really, Really Love You - Vol. 03 Ch. 023.5 - Volume 3 Extras.cbz", "The 100 Girlfriends Who Really, Really, Really, Really, Really Love You")]
    [InlineData("Kimi no Koto ga Daidaidaidaidaisuki na 100-nin no Kanojo Chapter 1-10", "Kimi no Koto ga Daidaidaidaidaisuki na 100-nin no Kanojo")]
    [InlineData("The Duke of Death and His Black Maid - Ch. 177 - The Ball (3).cbz", "The Duke of Death and His Black Maid")]
    [InlineData("The Duke of Death and His Black Maid - Vol. 04 Ch. 054.5 - V4 Omake", "The Duke of Death and His Black Maid")]
    [InlineData("Vol. 04 Ch. 054.5", "")]
    [InlineData("Great_Teacher_Onizuka_v16[TheSpectrum]", "Great Teacher Onizuka")]
    [InlineData("[Renzokusei]_Kimi_wa_Midara_na_Boku_no_Joou_Ch5_Final_Chapter", "Kimi wa Midara na Boku no Joou")]
    [InlineData("Battle Royale, v01 (2000) [TokyoPop] [Manga-Sketchbook]", "Battle Royale")]
    [InlineData("Kaiju No. 8 036 (2021) (Digital)", "Kaiju No. 8")]
    [InlineData("Seraph of the End - Vampire Reign 093  (2020) (Digital) (LuCaZ).cbz", "Seraph of the End - Vampire Reign")]
    [InlineData("Love Hina - Volume 01 [Scans].pdf", "Love Hina")]
    [InlineData("It's Witching Time! 001 (Digital) (Anonymous1234)", "It's Witching Time!")]
    [InlineData("Zettai Karen Children v02 c003 - The Invisible Guardian (2) [JS Scans]", "Zettai Karen Children")]
    [InlineData("My Charms Are Wasted on Kuroiwa Medaka - Ch. 37.5 - Volume Extras", "My Charms Are Wasted on Kuroiwa Medaka")]
    [InlineData("Highschool of the Dead - Full Color Edition v02 [Uasaha] (Yen Press)", "Highschool of the Dead - Full Color Edition")]
    [InlineData("諌山創] 進撃の巨人 第23巻", "諌山創] 進撃の巨人")]
    [InlineData("(一般コミック) [奥浩哉] いぬやしき 第09巻", "いぬやしき")]
    [InlineData("Highschool of the Dead - 02", "Highschool of the Dead")]
    [InlineData("Kebab Том 1 Глава 3", "Kebab")]
    [InlineData("Манга Глава 2", "Манга")]
    [InlineData("Манга Глава 2-2", "Манга")]
    [InlineData("Манга Том 1 3-4 Глава", "Манга")]
    [InlineData("Esquire 6권 2021년 10월호", "Esquire")]
    [InlineData("Accel World: Vol 1", "Accel World")]
    [InlineData("Accel World Chapter 001 Volume 002", "Accel World")]
    [InlineData("Bleach 001-003", "Bleach")]
    [InlineData("Accel World Volume 2", "Accel World")]
    [InlineData("죠시라쿠! 2년 후 v01", "죠시라쿠! 2년 후")]
    [InlineData("죠시라쿠! 2년 후 1권", "죠시라쿠! 2년 후")]
    [InlineData("test 2 years 1권", "test 2 years")]
    [InlineData("test 2 years 1화", "test 2 years")]
    [InlineData("Nagasarete Airantou - Vol. 30 Ch. 187.5 - Vol.30 Omake", "Nagasarete Airantou")]
    [InlineData("Cynthia The Mission - c000 - c006 (v06)", "Cynthia The Mission")]
    [InlineData("เด็กคนนี้ขอลาออกจากการเป็นเจ้าของปราสาท เล่ม 1", "เด็กคนนี้ขอลาออกจากการเป็นเจ้าของปราสาท")]
    [InlineData("Max Level Returner เล่มที่ 5", "Max Level Returner")]
    [InlineData("หนึ่งความคิด นิจนิรันดร์ เล่ม 2", "หนึ่งความคิด นิจนิรันดร์")]
    public void ParseSeriesTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseSeries(filename, LibraryType.Manga));
    }

    [Theory]
    [InlineData("Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)", "1")]
    [InlineData("My Girlfriend Is Shobitch v01 - ch. 09 - pg. 008.png", "9")]
    [InlineData("Historys Strongest Disciple Kenichi_v11_c90-98.zip", "90-98")]
    [InlineData("B_Gata_H_Kei_v01[SlowManga&OverloadScans]", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("BTOOOM! v01 (2013) (Digital) (Shadowcat-Empire)", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("Gokukoku no Brynhildr - c001-008 (v01) [TrinityBAKumA]", "1-8")]
    [InlineData("Dance in the Vampire Bund v16-17 (Digital) (NiceDragon)", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("c001", "1")]
    [InlineData("[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.12.zip", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
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
    [InlineData("Vol 1", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
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
    [InlineData("Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U Extra Chapter.rar", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("Beelzebub_153b_RHS.zip", "153.5")]
    [InlineData("Beelzebub_150-153b_RHS.zip", "150-153.5")]
    [InlineData("Transferred to another world magical swordsman v1.1", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("Kiss x Sis - Ch.15 - The Angst of a 15 Year Old Boy.cbz", "15")]
    [InlineData("Kiss x Sis - Ch.12 - 1 , 2 , 3P!.cbz", "12")]
    [InlineData("Umineko no Naku Koro ni - Episode 1 - Legend of the Golden Witch #1", "1")]
    [InlineData("Kiss x Sis - Ch.00 - Let's Start from 0.cbz", "0")]
    [InlineData("[Hidoi]_Amaenaideyo_MS_vol01_chp02.rar", "2")]
    [InlineData("Okusama wa Shougakusei c003 (v01) [bokuwaNEET]", "3")]
    [InlineData("Tomogui Kyoushitsu - Chapter 006 Game 005 - Fingernails On Right Hand (Part 002).cbz", "6")]
    [InlineData("Noblesse - Episode 406 (52 Pages).7z", "406")]
    [InlineData("X-Men v1 #201 (September 2007).cbz", "201")]
    [InlineData("Kodoja #001 (March 2016)", "1")]
    [InlineData("Noblesse - Episode 429 (74 Pages).7z", "429")]
    [InlineData("Boku No Kokoro No Yabai Yatsu - Chapter 054 I Prayed At The Shrine (V0).cbz", "54")]
    [InlineData("Ijousha No Ai - Vol.01 Chapter 029 8 Years Ago", "29")]
    [InlineData("Kedouin Makoto - Corpse Party Musume, Chapter 09.cbz", "9")]
    [InlineData("Hentai Ouji to Warawanai Neko. - Vol. 06 Ch. 034.5", "34.5")]
    [InlineData("Kimi no Koto ga Daidaidaidaidaisuki na 100-nin no Kanojo Chapter 1-10", "1-10")]
    [InlineData("Deku_&_Bakugo_-_Rising_v1_c1.1.cbz", "1.1")]
    [InlineData("Chapter 63 - The Promise Made for 520 Cenz.cbr", "63")]
    [InlineData("Harrison, Kim - The Good, The Bad, and the Undead - Hollows Vol 2.5.epub", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("Kaiju No. 8 036 (2021) (Digital)", "36")]
    [InlineData("Samurai Jack Vol. 01 - The threads of Time", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("【TFO汉化&Petit汉化】迷你偶像漫画第25话", "25")]
    [InlineData("자유록 13회#2", "13")]
    [InlineData("이세계에서 고아원을 열었지만, 어째서인지 아무도 독립하려 하지 않는다 38-1화 ", "38")]
    [InlineData("[ハレム]ナナとカオル ～高校生のSMごっこ～　第10話", "10")]
    [InlineData("Dance in the Vampire Bund {Special Edition} v03.5 (2019) (Digital) (KG Manga)", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("Kebab Том 1 Глава 3", "3")]
    [InlineData("Манга Глава 2", "2")]
    [InlineData("Манга 2 Глава", "2")]
    [InlineData("Манга Том 1 2 Глава", "2")]
    [InlineData("Accel World Chapter 001 Volume 002", "1")]
    [InlineData("Bleach 001-003", "1-3")]
    [InlineData("Accel World Volume 2", API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)]
    [InlineData("Historys Strongest Disciple Kenichi_v11_c90-98", "90-98")]
    [InlineData("Historys Strongest Disciple Kenichi c01-c04", "1-4")]
    [InlineData("Adabana c00-02", "0-2")]
    [InlineData("เด็กคนนี้ขอลาออกจากการเป็นเจ้าของปราสาท เล่ม 1 ตอนที่ 3", "3")]
    [InlineData("Max Level Returner ตอนที่ 5", "5")]
    [InlineData("หนึ่งความคิด นิจนิรันดร์ บทที่ 112", "112")]
    public void ParseChaptersTest(string filename, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseChapter(filename, LibraryType.Manga));
    }


    [Theory]
    [InlineData("Tenjou Tenge Omnibus", "Omnibus")]
    [InlineData("Tenjou Tenge {Full Contact Edition}", "")]
    [InlineData("Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz", "")]
    [InlineData("Wotakoi - Love is Hard for Otaku Omnibus v01 (2018) (Digital) (danke-Empire)", "Omnibus")]
    [InlineData("To Love Ru v01 Uncensored (Ch.001-007)", "Uncensored")]
    [InlineData("Chobits Omnibus Edition v01 [Dark Horse]", "Omnibus Edition")]
    [InlineData("Chobits_Omnibus_Edition_v01_[Dark_Horse]", "Omnibus Edition")]
    [InlineData("[dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz", "")]
    [InlineData("AKIRA - c003 (v01) [Full Color] [Darkhorse].cbz", "")]
    [InlineData("Love Hina Omnibus v05 (2015) (Digital-HD) (Asgard-Empire).cbz", "Omnibus")]
    public void ParseEditionTest(string input, string expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseEdition(input));
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
    [InlineData("Yuki Merry - 4-Komga Anthology", false)]
    [InlineData("Beastars - SP01", false)]
    [InlineData("Beastars SP01", false)]
    [InlineData("The League of Extraordinary Gentlemen", false)]
    [InlineData("The League of Extra-ordinary Gentlemen", false)]
    [InlineData("Dr. Ramune - Mysterious Disease Specialist v01 (2020) (Digital) (danke-Empire)", false)]
    [InlineData("Hajime no Ippo - Artbook", false)]
    public void IsMangaSpecialTest(string input, bool expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.IsSpecial(input, LibraryType.Manga));
    }

    [Theory]
    [InlineData("image.png", MangaFormat.Image)]
    [InlineData("image.cbz", MangaFormat.Archive)]
    [InlineData("image.txt", MangaFormat.Unknown)]
    public void ParseFormatTest(string inputFile, MangaFormat expected)
    {
        Assert.Equal(expected, API.Services.Tasks.Scanner.Parser.Parser.ParseFormat(inputFile));
    }


}
