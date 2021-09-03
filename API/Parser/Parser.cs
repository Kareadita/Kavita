using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using API.Entities.Enums;
using API.Services;

namespace API.Parser
{
    public static class Parser
    {
        public const string DefaultChapter = "0";
        public const string DefaultVolume = "0";
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

        public const string ImageFileExtensions = @"^(\.png|\.jpeg|\.jpg)";
        public const string ArchiveFileExtensions = @"\.cbz|\.zip|\.rar|\.cbr|\.tar.gz|\.7zip|\.7z|\.cb7|\.cbt";
        public const string BookFileExtensions = @"\.epub|\.pdf";
        public const string MacOsMetadataFileStartsWith = @"._";

        public const string SupportedExtensions =
            ArchiveFileExtensions + "|" + ImageFileExtensions + "|" + BookFileExtensions;

        public static readonly Regex FontSrcUrlRegex = new Regex(@"(src:url\(.{1})" + "([^\"']*)" + @"(.{1}\))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout);
        public static readonly Regex CssImportUrlRegex = new Regex("(@import\\s[\"|'])(?<Filename>[\\w\\d/\\._-]+)([\"|'];?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout);

        private static readonly string XmlRegexExtensions = @"\.xml";
        private static readonly Regex ImageRegex = new Regex(ImageFileExtensions,
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout);
        private static readonly Regex ArchiveFileRegex = new Regex(ArchiveFileExtensions,
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout);
        private static readonly Regex XmlRegex = new Regex(XmlRegexExtensions,
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout);
        private static readonly Regex BookFileRegex = new Regex(BookFileExtensions,
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout);
        private static readonly Regex CoverImageRegex = new Regex(@"(?<![[a-z]\d])(?:!?)(cover|folder)(?![\w\d])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout);


        private static readonly Regex[] MangaVolumeRegex = new[]
        {
            // Dance in the Vampire Bund v16-17
            new Regex(
                @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d+)( |_)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // NEEDLESS_Vol.4_-Simeon_6_v2[SugoiSugoi].rar
            new Regex(
                @"(?<Series>.*)(\b|_)(?!\[)(vol\.?)(?<Volume>\d+(-\d+)?)(?!\])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip or Dance in the Vampire Bund v16-17
            new Regex(
                @"(?<Series>.*)(\b|_)(?!\[)v(?<Volume>\d+(-\d+)?)(?!\])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Kodomo no Jikan vol. 10
            new Regex(
                @"(?<Series>.*)(\b|_)(vol\.? ?)(?<Volume>\d+(-\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)
            new Regex(
                @"(vol\.? ?)(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Tonikaku Cawaii [Volume 11].cbz
            new Regex(
                @"(volume )(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Tower Of God S01 014 (CBT) (digital).cbz
            new Regex(
                @"(?<Series>.*)(\b|_|)(S(?<Volume>\d+))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // vol_001-1.cbz for MangaPy default naming convention
            new Regex(
                @"(vol_)(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
        };

        private static readonly Regex[] MangaSeriesRegex = new[]
        {
            // Grand Blue Dreaming - SP02
            new Regex(
                @"(?<Series>.*)(\b|_|-|\s)(?:sp)\d",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // [SugoiSugoi]_NEEDLESS_Vol.2_-_Disk_The_Informant_5_[ENG].rar, Yuusha Ga Shinda! - Vol.tbd Chapter 27.001 V2 Infection ①.cbz
            new Regex(
                @"^(?<Series>.*)( |_)Vol\.?(\d+|tbd)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Ichiban_Ushiro_no_Daimaou_v04_ch34_[VISCANS].zip, VanDread-v01-c01.zip
            new Regex(
            @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d*)(\s|_|-)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Gokukoku no Brynhildr - c001-008 (v01) [TrinityBAKumA], Black Bullet - v4 c17 [batoto]
            new Regex(
                @"(?<Series>.*)( - )(?:v|vo|c)\d",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Kedouin Makoto - Corpse Party Musume, Chapter 19 [Dametrans].zip
            new Regex(
                @"(?<Series>.*)(?:, Chapter )(?<Chapter>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Mad Chimera World - Volume 005 - Chapter 026.cbz (couldn't figure out how to get Volume negative lookaround working on below regex)
            new Regex(
                @"(?<Series>.*)(\s|_|-)(?:Volume(\s|_|-)+\d+)(\s|_|-)+(?:Chapter)(\s|_|-)(?<Chapter>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Please Go Home, Akutsu-San! - Chapter 038.5 - Volume Announcement.cbz
            new Regex(
                @"(?<Series>.*)(\s|_|-)(?!Vol)(\s|_|-)(?:Chapter)(\s|_|-)(?<Chapter>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // [dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz
            new Regex(
                @"(?<Series>.*) (\b|_|-)(vol)\.?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            //Knights of Sidonia c000 (S2 LE BD Omake - BLAME!) [Habanero Scans]
            new Regex(
                @"(?<Series>.*)(\bc\d+\b)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            //Tonikaku Cawaii [Volume 11], Darling in the FranXX - Volume 01.cbz
            new Regex(
                @"(?<Series>.*)(?: _|-|\[|\()\s?vol(ume)?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Momo The Blood Taker - Chapter 027 Violent Emotion.cbz, Grand Blue Dreaming - SP02 Extra (2019) (Digital) (danke-Empire).cbz
            new Regex(
                @"(?<Series>.*)(\b|_|-|\s)(?:(chapter(\b|_|-|\s))|sp)\d",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip, Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)
            new Regex(
                @"(?<Series>.*) (\b|_|-)(v|ch\.?|c)\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            //Ichinensei_ni_Nacchattara_v01_ch01_[Taruby]_v1.1.zip must be before [Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            // due to duplicate version identifiers in file.
            new Regex(
                @"(?<Series>.*)(v|s)\d+(-\d+)?(_|\s)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            //[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            new Regex(
                @"(?<Series>.*)(v|s)\d+(-\d+)?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz
            new Regex(
                @"(?<Series>.*) (?<Chapter>\d+) (?:\(\d{4}\)) ",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire)
            new Regex(
                @"(?<Series>.*) (?<Chapter>\d+(?:.\d+|-\d+)?) \(\d{4}\)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Noblesse - Episode 429 (74 Pages).7z
            new Regex(
                @"(?<Series>.*)(\s|_)(?:Episode|Ep\.?)(\s|_)(?<Chapter>\d+(?:.\d+|-\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Akame ga KILL! ZERO (2016-2019) (Digital) (LuCaZ)
            new Regex(
                @"(?<Series>.*)\(\d",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Tonikaku Kawaii (Ch 59-67) (Ongoing)
            new Regex(
                @"(?<Series>.*)(\s|_)\((c\s|ch\s|chapter\s)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Black Bullet (This is very loose, keep towards bottom)
            new Regex(
                @"(?<Series>.*)(_)(v|vo|c|volume)( |_)\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // [Hidoi]_Amaenaideyo_MS_vol01_chp02.rar
            new Regex(
                @"(?<Series>.*)( |_)(vol\d+)?( |_)(?:Chp\.? ?\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Mahoutsukai to Deshi no Futekisetsu na Kankei Chp. 1
            new Regex(
                @"(?<Series>.*)( |_)(?:Chp.? ?\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U Chapter 01
            new Regex(
                @"^(?!Vol)(?<Series>.*)( |_)Chapter( |_)(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),

            // Fullmetal Alchemist chapters 101-108.cbz
            new Regex(
                @"^(?!vol)(?<Series>.*)( |_)(chapters( |_)?)\d+-?\d*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Umineko no Naku Koro ni - Episode 1 - Legend of the Golden Witch #1
            new Regex(
                @"^(?!Vol\.?)(?<Series>.*)( |_|-)(?<!-)(episode|chapter|(ch\.?) ?)\d+-?\d*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),

            // Baketeriya ch01-05.zip
            new Regex(
                @"^(?!Vol)(?<Series>.*)ch\d+-?\d?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Magi - Ch.252-005.cbz
            new Regex(
                @"(?<Series>.*)( ?- ?)Ch\.\d+-?\d*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // [BAA]_Darker_than_Black_Omake-1.zip
            new Regex(
                @"^(?!Vol)(?<Series>.*)(-)\d+-?\d*", // This catches a lot of stuff ^(?!Vol)(?<Series>.*)( |_)(\d+)
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Kodoja #001 (March 2016)
            new Regex(
                @"(?<Series>.*)(\s|_|-)#",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Baketeriya ch01-05.zip, Akiiro Bousou Biyori - 01.jpg, Beelzebub_172_RHS.zip, Cynthia the Mission 29.rar
            new Regex(
                @"^(?!Vol\.?)(?<Series>.*)( |_|-)(?<!-)(ch)?\d+-?\d*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // [BAA]_Darker_than_Black_c1 (This is very greedy, make sure it's close to last)
            new Regex(
                @"^(?!Vol)(?<Series>.*)( |_|-)(ch?)\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
        };

        private static readonly Regex[] ComicSeriesRegex = new[]
        {
            // Invincible Vol 01 Family matters (2005) (Digital)
            new Regex(
                @"(?<Series>.*)(\b|_)(vol\.?)( |_)(?<Volume>\d+(-\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // 04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)
            new Regex(
            @"^(?<Volume>\d+) (- |_)?(?<Series>.*(\d{4})?)( |_)(\(|\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // 01 Spider-Man & Wolverine 01.cbr
            new Regex(
            @"^(?<Volume>\d+) (?:- )?(?<Series>.*) (\d+)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Batman & Wildcat (1 of 3)
            new Regex(
            @"(?<Series>.*(\d{4})?)( |_)(?:\((?<Volume>\d+) of \d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: |_)v\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Amazing Man Comics chapter 25
            new Regex(
                @"^(?<Series>.*)(?: |_)c(hapter) \d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Amazing Man Comics issue #25
            new Regex(
                @"^(?<Series>.*)(?: |_)i(ssue) #\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Batman & Catwoman - Trail of the Gun 01, Batman & Grendel (1996) 01 - Devil's Bones, Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: \d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Batman & Robin the Teen Wonder #0
            new Regex(
                @"^(?<Series>.*)(?: |_)#\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Scott Pilgrim 02 - Scott Pilgrim vs. The World (2005)
            new Regex(
                @"^(?<Series>.*)(?: |_)(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // The First Asterix Frieze (WebP by Doc MaKS)
            new Regex(
                @"^(?<Series>.*)(?: |_)(?!\(\d{4}|\d{4}-\d{2}\))\(",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // MUST BE LAST: Batman & Daredevil - King of New York
            new Regex(
                @"^(?<Series>.*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
        };

        private static readonly Regex[] ComicVolumeRegex = new[]
        {
            // 04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)
            new Regex(
                @"^(?<Volume>\d+) (- |_)?(?<Series>.*(\d{4})?)( |_)(\(|\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // 01 Spider-Man & Wolverine 01.cbr
            new Regex(
                @"^(?<Volume>\d+) (?:- )?(?<Series>.*) (\d+)?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Batman & Wildcat (1 of 3)
            new Regex(
                @"(?<Series>.*(\d{4})?)( |_)(?:\((?<Chapter>\d+) of \d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: |_)v(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Scott Pilgrim 02 - Scott Pilgrim vs. The World (2005)
            new Regex(
                @"^(?<Series>.*)(?<!c(hapter)|i(ssue))(?<!of)(?: |_)(?<!of )(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Batman & Catwoman - Trail of the Gun 01, Batman & Grendel (1996) 01 - Devil's Bones, Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?<!c(hapter)|i(ssue))(?<!of)(?: (?<Volume>\d+))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Batman & Robin the Teen Wonder #0
            new Regex(
                @"^(?<Series>.*)(?: |_)#(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
        };

        private static readonly Regex[] ComicChapterRegex = new[]
        {
          // Batman & Wildcat (1 of 3)
            new Regex(
                @"(?<Series>.*(\d{4})?)( |_)(?:\((?<Chapter>\d+) of \d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: |_)v(?<Volume>\d+)(?: |_)(c? ?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)(c? ?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Batman & Catwoman - Trail of the Gun 01, Batman & Grendel (1996) 01 - Devil's Bones, Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: (?<Volume>\d+))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Batman & Robin the Teen Wonder #0
            new Regex(
                @"^(?<Series>.*)(?: |_)#(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Invincible 070.5 - Invincible Returns 1 (2010) (digital) (Minutemen-InnerDemons).cbr
            new Regex(
                @"^(?<Series>.*)(?: |_)(c? ?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)(c? ?)-",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Amazing Man Comics chapter 25
            new Regex(
                @"^(?!Vol)(?<Series>.*)( |_)c(hapter)( |_)(?<Chapter>\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Amazing Man Comics issue #25
            new Regex(
                @"^(?!Vol)(?<Series>.*)( |_)i(ssue)( |_) #(?<Chapter>\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
        };

        private static readonly Regex[] ReleaseGroupRegex = new[]
        {
            // [TrinityBAKumA Finella&anon], [BAA]_, [SlowManga&OverloadScans], [batoto]
            new Regex(@"(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // (Shadowcat-Empire),
            // new Regex(@"(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
            //     RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex[] MangaChapterRegex = new[]
        {
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip, ...c90.5-100.5
            new Regex(
                @"(\b|_)(c|ch)(\.?\s?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // [Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            new Regex(
                @"v\d+\.(?<Chapter>\d+(?:.\d+|-\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Umineko no Naku Koro ni - Episode 3 - Banquet of the Golden Witch #02.cbz (Rare case, if causes issue remove)
            new Regex(
                @"^(?<Series>.*)(?: |_)#(?<Chapter>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Green Worldz - Chapter 027
            new Regex(
                @"^(?!Vol)(?<Series>.*)\s?(?<!vol\. )\sChapter\s(?<Chapter>\d+(?:\.?[\d-])?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz, Hinowa ga CRUSH! 018.5 (2019) (Digital) (LuCaZ).cbz
            new Regex(
                @"^(?!Vol)(?<Series>.*)\s(?<!vol\. )(?<Chapter>\d+(?:.\d+|-\d+)?)(?:\s\(\d{4}\))?(\b|_|-)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Tower Of God S01 014 (CBT) (digital).cbz
            new Regex(
                @"(?<Series>.*)\sS(?<Volume>\d+)\s(?<Chapter>\d+(?:.\d+|-\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Beelzebub_01_[Noodles].zip, Beelzebub_153b_RHS.zip
            new Regex(
                @"^((?!v|vo|vol|Volume).)*(\s|_)(?<Chapter>\.?\d+(?:.\d+|-\d+)?)(?<ChapterPart>b)?(\s|_|\[|\()",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Yumekui-Merry_DKThias_Chapter21.zip
            new Regex(
                @"Chapter(?<Chapter>\d+(-\d+)?)", //(?:.\d+|-\d+)?
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // [Hidoi]_Amaenaideyo_MS_vol01_chp02.rar
            new Regex(
                @"(?<Series>.*)(\s|_)(vol\d+)?(\s|_)Chp\.? ?(?<Chapter>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Vol 1 Chapter 2
            new Regex(
              @"(?<Volume>((vol|volume|v))?(\s|_)?\.?\d+)(\s|_)(Chp|Chapter)\.?(\s|_)?(?<Chapter>\d+)",
              RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),

        };
        private static readonly Regex[] MangaEditionRegex = {
            // Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz
            new Regex(
                @"(?<Edition>({|\(|\[).* Edition(}|\)|\]))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz
            new Regex(
                @"(\b|_)(?<Edition>Omnibus(( |_)?Edition)?)(\b|_)?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // To Love Ru v01 Uncensored (Ch.001-007)
            new Regex(
                @"(\b|_)(?<Edition>Uncensored)(\b|_)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // AKIRA - c003 (v01) [Full Color] [Darkhorse].cbz
            new Regex(
                @"(\b|_)(?<Edition>Full(?: |_)Color)(\b|_)?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
        };

        private static readonly Regex[] CleanupRegex =
        {
            // (), {}, []
            new Regex(
                @"(?<Cleanup>(\{\}|\[\]|\(\)))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // (Complete)
            new Regex(
                @"(?<Cleanup>(\{Complete\}|\[Complete\]|\(Complete\)))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
            // Anything in parenthesis
            new Regex(
                @"\(.*\)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
        };

        private static readonly Regex[] MangaSpecialRegex =
        {
            // All Keywords, does not account for checking if contains volume/chapter identification. Parser.Parse() will handle.
            new Regex(
                @"(?<Special>Specials?|OneShot|One\-Shot|Omake|Extra( Chapter)?|Art Collection|Side( |_)Stories|Bonus)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout),
        };

        // If SP\d+ is in the filename, we force treat it as a special regardless if volume or chapter might have been found.
        private static readonly Regex SpecialMarkerRegex = new Regex(
            @"(?<Special>SP\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            RegexTimeout
        );


        /// <summary>
        /// Parses information out of a file path. Will fallback to using directory name if Series couldn't be parsed
        /// from filename.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="rootPath">Root folder</param>
        /// <param name="type">Defaults to Manga. Allows different Regex to be used for parsing.</param>
        /// <returns><see cref="ParserInfo"/> or null if Series was empty</returns>
        public static ParserInfo Parse(string filePath, string rootPath, LibraryType type = LibraryType.Manga)
        {
            var fileName = Path.GetFileName(filePath);
            ParserInfo ret;

            if (IsEpub(filePath))
            {
                ret = new ParserInfo()
                {
                    Chapters = ParseChapter(fileName) ?? ParseComicChapter(fileName),
                    Series = ParseSeries(fileName) ?? ParseComicSeries(fileName),
                    Volumes = ParseVolume(fileName) ?? ParseComicVolume(fileName),
                    Filename = fileName,
                    Format = ParseFormat(filePath),
                    FullFilePath = filePath
                };
            }
            else
            {
                ret = new ParserInfo()
                {
                    Chapters = type == LibraryType.Manga ? ParseChapter(fileName) : ParseComicChapter(fileName),
                    Series = type == LibraryType.Manga ? ParseSeries(fileName) : ParseComicSeries(fileName),
                    Volumes = type == LibraryType.Manga ? ParseVolume(fileName) : ParseComicVolume(fileName),
                    Filename = fileName,
                    Format = ParseFormat(filePath),
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    FullFilePath = filePath
                };
            }

            if (IsImage(filePath) && IsCoverImage(fileName)) return null;

            if (IsImage(filePath))
            {
              // Reset Chapters, Volumes, and Series as images are not good to parse information out of. Better to use folders.
              ret.Volumes = DefaultVolume;
              ret.Chapters = DefaultChapter;
              ret.Series = string.Empty;
            }

            if (ret.Series == string.Empty || IsImage(filePath))
            {
                // Try to parse information out of each folder all the way to rootPath
                ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
            }

            var edition = ParseEdition(fileName);
            if (!string.IsNullOrEmpty(edition))
            {
                ret.Series = CleanTitle(ret.Series.Replace(edition, ""));
                ret.Edition = edition;
            }

            var isSpecial = ParseMangaSpecial(fileName);
            // We must ensure that we can only parse a special out. As some files will have v20 c171-180+Omake and that
            // could cause a problem as Omake is a special term, but there is valid volume/chapter information.
            if (ret.Chapters == DefaultChapter && ret.Volumes == DefaultVolume && !string.IsNullOrEmpty(isSpecial))
            {
                ret.IsSpecial = true;
            }

            if (HasSpecialMarker(fileName))
            {
                ret.IsSpecial = true;
                ret.Chapters = DefaultChapter;
                ret.Volumes = DefaultVolume;

                ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
            }
            // here is the issue. If we are a special with marker, we need to ensure we use the correct series name.
            // we can do this by falling back

            if (string.IsNullOrEmpty(ret.Series))
            {
                ret.Series = CleanTitle(fileName);
            }

            // Pdfs may have .pdf in the series name, remove that
            if (IsPdf(fileName) && ret.Series.ToLower().EndsWith(".pdf"))
            {
                ret.Series = ret.Series.Substring(0, ret.Series.Length - ".pdf".Length);
            }

            return ret.Series == string.Empty ? null : ret;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="rootPath"></param>
        /// <param name="type"></param>
        /// <param name="ret">Expects a non-null ParserInfo which this method will populate</param>
        public static void ParseFromFallbackFolders(string filePath, string rootPath, LibraryType type, ref ParserInfo ret)
        {
          var fallbackFolders = DirectoryService.GetFoldersTillRoot(rootPath, filePath).ToList();
            for (var i = 0; i < fallbackFolders.Count; i++)
            {
                var folder = fallbackFolders[i];
                if (!string.IsNullOrEmpty(ParseMangaSpecial(folder))) continue;

                var parsedVolume = type is LibraryType.Manga ? ParseVolume(folder) : ParseComicVolume(folder);
                var parsedChapter = type is LibraryType.Manga ? ParseChapter(folder) : ParseComicChapter(folder);

                if (!parsedVolume.Equals(DefaultVolume) || !parsedChapter.Equals(DefaultChapter))
                {
                  if ((ret.Volumes.Equals(DefaultVolume) || string.IsNullOrEmpty(ret.Volumes)) && !parsedVolume.Equals(DefaultVolume))
                  {
                    ret.Volumes = parsedVolume;
                  }
                  if ((ret.Chapters.Equals(DefaultChapter) || string.IsNullOrEmpty(ret.Chapters)) && !parsedChapter.Equals(DefaultChapter))
                  {
                    ret.Chapters = parsedChapter;
                  }

                  continue;
                }

                var series = ParseSeries(folder);

                if ((string.IsNullOrEmpty(series) && i == fallbackFolders.Count - 1))
                {
                    ret.Series = CleanTitle(folder);
                    break;
                }

                if (!string.IsNullOrEmpty(series))
                {
                    ret.Series = series;
                    break;
                }
            }
        }

        public static MangaFormat ParseFormat(string filePath)
        {
            if (IsArchive(filePath)) return MangaFormat.Archive;
            if (IsImage(filePath)) return MangaFormat.Image;
            if (IsEpub(filePath)) return MangaFormat.Epub;
            if (IsPdf(filePath)) return MangaFormat.Pdf;
            return MangaFormat.Unknown;
        }

        public static string ParseEdition(string filePath)
        {
            foreach (var regex in MangaEditionRegex)
            {
                var matches = regex.Matches(filePath);
                foreach (Match match in matches)
                {
                    if (match.Groups["Edition"].Success && match.Groups["Edition"].Value != string.Empty)
                    {
                        var edition = match.Groups["Edition"].Value.Replace("{", "").Replace("}", "")
                            .Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "");

                        return edition;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// If the file has SP marker.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static bool HasSpecialMarker(string filePath)
        {
            var matches = SpecialMarkerRegex.Matches(filePath);
            foreach (Match match in matches)
            {
                if (match.Groups["Special"].Success && match.Groups["Special"].Value != string.Empty)
                {
                    return true;
                }
            }

            return false;
        }

        public static string ParseMangaSpecial(string filePath)
        {
            foreach (var regex in MangaSpecialRegex)
            {
                var matches = regex.Matches(filePath);
                foreach (Match match in matches)
                {
                    if (match.Groups["Special"].Success && match.Groups["Special"].Value != string.Empty)
                    {
                        return match.Groups["Special"].Value;
                    }
                }
            }

            return string.Empty;
        }

        public static string ParseSeries(string filename)
        {
            foreach (var regex in MangaSeriesRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (match.Groups["Series"].Success && match.Groups["Series"].Value != string.Empty)
                    {
                        return CleanTitle(match.Groups["Series"].Value);
                    }
                }
            }

            return string.Empty;
        }
        public static string ParseComicSeries(string filename)
        {
            foreach (var regex in ComicSeriesRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (match.Groups["Series"].Success && match.Groups["Series"].Value != string.Empty)
                    {
                        return CleanTitle(match.Groups["Series"].Value);
                    }
                }
            }

            return string.Empty;
        }

        public static string ParseVolume(string filename)
        {
            foreach (var regex in MangaVolumeRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (!match.Groups["Volume"].Success || match.Groups["Volume"] == Match.Empty) continue;

                    var value = match.Groups["Volume"].Value;
                    if (!value.Contains("-")) return RemoveLeadingZeroes(match.Groups["Volume"].Value);
                    var tokens = value.Split("-");
                    var from = RemoveLeadingZeroes(tokens[0]);
                    var to = RemoveLeadingZeroes(tokens[1]);
                    return $"{@from}-{to}";

                }
            }

            return DefaultVolume;
        }

        public static string ParseComicVolume(string filename)
        {
            foreach (var regex in ComicVolumeRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (!match.Groups["Volume"].Success || match.Groups["Volume"] == Match.Empty) continue;

                    var value = match.Groups["Volume"].Value;
                    if (!value.Contains("-")) return RemoveLeadingZeroes(match.Groups["Volume"].Value);
                    var tokens = value.Split("-");
                    var from = RemoveLeadingZeroes(tokens[0]);
                    var to = RemoveLeadingZeroes(tokens[1]);
                    return $"{@from}-{to}";

                }
            }

            return DefaultVolume;
        }

        public static string ParseChapter(string filename)
        {
            foreach (var regex in MangaChapterRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (!match.Groups["Chapter"].Success || match.Groups["Chapter"] == Match.Empty) continue;

                    var value = match.Groups["Chapter"].Value;
                    var hasChapterPart = match.Groups["ChapterPart"].Success;

                    if (!value.Contains("-"))
                    {
                        return RemoveLeadingZeroes(hasChapterPart ? AddChapterPart(value) : value);
                    }

                    var tokens = value.Split("-");
                    var from = RemoveLeadingZeroes(tokens[0]);
                    var to = RemoveLeadingZeroes(hasChapterPart ? AddChapterPart(tokens[1]) : tokens[1]);
                    return $"{@from}-{to}";

                }
            }

            return DefaultChapter;
        }

        private static string AddChapterPart(string value)
        {
            if (value.Contains("."))
            {
                return value;
            }

            return $"{value}.5";
        }

        public static string ParseComicChapter(string filename)
        {
            foreach (var regex in ComicChapterRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (match.Groups["Chapter"].Success && match.Groups["Chapter"] != Match.Empty)
                    {
                        var value = match.Groups["Chapter"].Value;

                        if (value.Contains("-"))
                        {
                            var tokens = value.Split("-");
                            var from = RemoveLeadingZeroes(tokens[0]);
                            var to = RemoveLeadingZeroes(tokens[1]);
                            return $"{from}-{to}";
                        }

                        return RemoveLeadingZeroes(match.Groups["Chapter"].Value);
                    }

                }
            }

            return DefaultChapter;
        }

        private static string RemoveEditionTagHolders(string title)
        {
            foreach (var regex in CleanupRegex)
            {
                var matches = regex.Matches(title);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        title = title.Replace(match.Value, "").Trim();
                    }
                }
            }

            foreach (var regex in MangaEditionRegex)
            {
                var matches = regex.Matches(title);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        title = title.Replace(match.Value, "").Trim();
                    }
                }
            }

            return title;
        }

        private static string RemoveSpecialTags(string title)
        {
            foreach (var regex in MangaSpecialRegex)
            {
                var matches = regex.Matches(title);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        title = title.Replace(match.Value, "").Trim();
                    }
                }
            }

            return title;
        }



        /// <summary>
        /// Translates _ -> spaces, trims front and back of string, removes release groups
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public static string CleanTitle(string title)
        {
            title = RemoveReleaseGroup(title);

            title = RemoveEditionTagHolders(title);

            title = RemoveSpecialTags(title);

            title = title.Replace("_", " ").Trim();
            if (title.EndsWith("-"))
            {
                title = title.Substring(0, title.Length - 1);
            }

            return title.Trim();
        }

        private static string RemoveReleaseGroup(string title)
        {
            foreach (var regex in ReleaseGroupRegex)
            {
                var matches = regex.Matches(title);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        title = title.Replace(match.Value, "");
                    }
                }
            }

            return title;
        }


        /// <summary>
        /// Pads the start of a number string with 0's so ordering works fine if there are over 100 items.
        /// Handles ranges (ie 4-8) -> (004-008).
        /// </summary>
        /// <param name="number"></param>
        /// <returns>A zero padded number</returns>
        public static string PadZeros(string number)
        {
            if (number.Contains("-"))
            {
                var tokens = number.Split("-");
                return $"{PerformPadding(tokens[0])}-{PerformPadding(tokens[1])}";
            }

            return PerformPadding(number);
        }

        private static string PerformPadding(string number)
        {
            var num = Int32.Parse(number);
            return num switch
            {
                < 10 => "00" + num,
                < 100 => "0" + num,
                _ => number
            };
        }

        public static string RemoveLeadingZeroes(string title)
        {
            var ret = title.TrimStart(new[] { '0' });
            return ret == string.Empty ? "0" : ret;
        }

        public static bool IsArchive(string filePath)
        {
            return ArchiveFileRegex.IsMatch(Path.GetExtension(filePath));
        }
        public static bool IsBook(string filePath)
        {
            return BookFileRegex.IsMatch(Path.GetExtension(filePath));
        }

        public static bool IsImage(string filePath, bool suppressExtraChecks = false)
        {
            if (filePath.StartsWith(".") || (!suppressExtraChecks && filePath.StartsWith("!"))) return false;
            return ImageRegex.IsMatch(Path.GetExtension(filePath));
        }

        public static bool IsXml(string filePath)
        {
            return XmlRegex.IsMatch(Path.GetExtension(filePath));
        }

        public static float MinimumNumberFromRange(string range)
        {
            try
            {
                if (!Regex.IsMatch(range, @"^[\d-.]+$"))
                {
                    return (float) 0.0;
                }

                var tokens = range.Replace("_", string.Empty).Split("-");
                return tokens.Min(float.Parse);
            }
            catch
            {
                return (float) 0.0;
            }
        }

        public static string Normalize(string name)
        {
            return Regex.Replace(name.ToLower(), "[^a-zA-Z0-9]", string.Empty);
        }


        /// <summary>
        /// Tests whether the file is a cover image such that: contains "cover", is named "folder", and is an image
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool IsCoverImage(string name)
        {
            return IsImage(name, true) && (CoverImageRegex.IsMatch(name));
        }

        public static bool HasBlacklistedFolderInPath(string path)
        {
            return path.Contains("__MACOSX");
        }


        public static bool IsEpub(string filePath)
        {
            return Path.GetExtension(filePath).ToLower() == ".epub";
        }

        public static bool IsPdf(string filePath)
        {
           return Path.GetExtension(filePath).ToLower() == ".pdf";
        }
    }
}
