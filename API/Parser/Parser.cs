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

        public const string ImageFileExtensions = @"^(\.png|\.jpeg|\.jpg|\.webp)";
        public const string ArchiveFileExtensions = @"\.cbz|\.zip|\.rar|\.cbr|\.tar.gz|\.7zip|\.7z|\.cb7|\.cbt";
        public const string BookFileExtensions = @"\.epub|\.pdf";
        public const string MacOsMetadataFileStartsWith = @"._";

        public const string SupportedExtensions =
            ArchiveFileExtensions + "|" + ImageFileExtensions + "|" + BookFileExtensions;

        private const RegexOptions MatchOptions =
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;

        /// <summary>
        /// Matches against font-family css syntax. Does not match if url import has data: starting, as that is binary data
        /// </summary>
        /// <remarks>See here for some examples https://developer.mozilla.org/en-US/docs/Web/CSS/@font-face</remarks>
        public static readonly Regex FontSrcUrlRegex = new Regex(@"(?<Start>(src:\s?)?url\((?!data:).(?!data:))" + "(?<Filename>(?!data:)[^\"']*)" + @"(?<End>.{1}\))",
            MatchOptions, RegexTimeout);
        /// <summary>
        /// https://developer.mozilla.org/en-US/docs/Web/CSS/@import
        /// </summary>
        public static readonly Regex CssImportUrlRegex = new Regex("(@import\\s([\"|']|url\\([\"|']))(?<Filename>[^'\"]+)([\"|']\\)?);",
            MatchOptions | RegexOptions.Multiline, RegexTimeout);
        /// <summary>
        /// Misc css image references, like background-image: url(), border-image, or list-style-image
        /// </summary>
        /// Original prepend: (background|border|list-style)-image:\s?)?
        public static readonly Regex CssImageUrlRegex = new Regex(@"(url\((?!data:).(?!data:))" + "(?<Filename>(?!data:)[^\"']*)" + @"(.\))",
            MatchOptions, RegexTimeout);


        private static readonly string XmlRegexExtensions = @"\.xml";
        private static readonly Regex ImageRegex = new Regex(ImageFileExtensions,
            MatchOptions, RegexTimeout);
        private static readonly Regex ArchiveFileRegex = new Regex(ArchiveFileExtensions,
            MatchOptions, RegexTimeout);
        private static readonly Regex XmlRegex = new Regex(XmlRegexExtensions,
            MatchOptions, RegexTimeout);
        private static readonly Regex BookFileRegex = new Regex(BookFileExtensions,
            MatchOptions, RegexTimeout);
        private static readonly Regex CoverImageRegex = new Regex(@"(?<![[a-z]\d])(?:!?)(cover|folder)(?![\w\d])",
            MatchOptions, RegexTimeout);

        private static readonly Regex NormalizeRegex = new Regex(@"[^a-zA-Z0-9\+]",
            MatchOptions, RegexTimeout);


        private static readonly Regex[] MangaVolumeRegex = new[]
        {
            // Dance in the Vampire Bund v16-17
            new Regex(
                @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d+)( |_)",
                MatchOptions, RegexTimeout),
            // NEEDLESS_Vol.4_-Simeon_6_v2[SugoiSugoi].rar
            new Regex(
                @"(?<Series>.*)(\b|_)(?!\[)(vol\.?)(?<Volume>\d+(-\d+)?)(?!\])",
                MatchOptions, RegexTimeout),
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip or Dance in the Vampire Bund v16-17
            new Regex(
                @"(?<Series>.*)(\b|_)(?!\[)v(?<Volume>\d+(-\d+)?)(?!\])",
                MatchOptions, RegexTimeout),
            // Kodomo no Jikan vol. 10, [dmntsf.net] One Piece - Digital Colored Comics Vol. 20.5-21.5 Ch. 177
            new Regex(
                @"(?<Series>.*)(\b|_)(vol\.? ?)(?<Volume>\d+(\.\d)?(-\d+)?(\.\d)?)",
                MatchOptions, RegexTimeout),
            // Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)
            new Regex(
                @"(vol\.? ?)(?<Volume>\d+(\.\d)?)",
                MatchOptions, RegexTimeout),
            // Tonikaku Cawaii [Volume 11].cbz
            new Regex(
                @"(volume )(?<Volume>\d+(\.\d)?)",
                MatchOptions, RegexTimeout),
            // Tower Of God S01 014 (CBT) (digital).cbz
            new Regex(
                @"(?<Series>.*)(\b|_|)(S(?<Volume>\d+))",
                MatchOptions, RegexTimeout),
            // vol_001-1.cbz for MangaPy default naming convention
            new Regex(
                @"(vol_)(?<Volume>\d+(\.\d)?)",
                MatchOptions, RegexTimeout),
        };

        private static readonly Regex[] MangaSeriesRegex = new[]
        {
            // Grand Blue Dreaming - SP02
            new Regex(
                @"(?<Series>.*)(\b|_|-|\s)(?:sp)\d",
                MatchOptions, RegexTimeout),
            // [SugoiSugoi]_NEEDLESS_Vol.2_-_Disk_The_Informant_5_[ENG].rar, Yuusha Ga Shinda! - Vol.tbd Chapter 27.001 V2 Infection ①.cbz
            new Regex(
                @"^(?<Series>.*)( |_)Vol\.?(\d+|tbd)",
                MatchOptions, RegexTimeout),
            // Mad Chimera World - Volume 005 - Chapter 026.cbz (couldn't figure out how to get Volume negative lookaround working on below regex),
            // The Duke of Death and His Black Maid - Vol. 04 Ch. 054.5 - V4 Omake
            new Regex(
                @"(?<Series>.+?)(\s|_|-)+(?:Vol(ume|\.)?(\s|_|-)+\d+)(\s|_|-)+(?:(Ch|Chapter|Ch)\.?)(\s|_|-)+(?<Chapter>\d+)",
                MatchOptions,
                RegexTimeout),
            // Ichiban_Ushiro_no_Daimaou_v04_ch34_[VISCANS].zip, VanDread-v01-c01.zip
            new Regex(
                @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d*)(\s|_|-)",
                MatchOptions,
                RegexTimeout),
            // Gokukoku no Brynhildr - c001-008 (v01) [TrinityBAKumA], Black Bullet - v4 c17 [batoto]
            new Regex(
                @"(?<Series>.*)( - )(?:v|vo|c)\d",
                MatchOptions, RegexTimeout),
            // Kedouin Makoto - Corpse Party Musume, Chapter 19 [Dametrans].zip
            new Regex(
                @"(?<Series>.*)(?:, Chapter )(?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // Please Go Home, Akutsu-San! - Chapter 038.5 - Volume Announcement.cbz
            new Regex(
                @"(?<Series>.*)(\s|_|-)(?!Vol)(\s|_|-)(?:Chapter)(\s|_|-)(?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // [dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz
            new Regex(
                @"(?<Series>.*) (\b|_|-)(vol)\.?(\s|-|_)?\d+",
                MatchOptions, RegexTimeout),
            // [xPearse] Kyochuu Rettou Volume 1 [English] [Manga] [Volume Scans]
            new Regex(
                @"(?<Series>.*) (\b|_|-)(vol)(ume)",
                MatchOptions,
                RegexTimeout),
            //Knights of Sidonia c000 (S2 LE BD Omake - BLAME!) [Habanero Scans]
            new Regex(
                @"(?<Series>.*)(\bc\d+\b)",
                MatchOptions, RegexTimeout),
            //Tonikaku Cawaii [Volume 11], Darling in the FranXX - Volume 01.cbz
            new Regex(
                @"(?<Series>.*)(?: _|-|\[|\()\s?vol(ume)?",
                MatchOptions, RegexTimeout),
            // Momo The Blood Taker - Chapter 027 Violent Emotion.cbz, Grand Blue Dreaming - SP02 Extra (2019) (Digital) (danke-Empire).cbz
            new Regex(
                @"^(?<Series>(?!Vol).+?)(?:(ch(apter|\.)(\b|_|-|\s))|sp)\d",
                MatchOptions, RegexTimeout),
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip, Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)
            new Regex(
                @"(?<Series>.*) (\b|_|-)(v|ch\.?|c)\d+",
                MatchOptions, RegexTimeout),
            //Ichinensei_ni_Nacchattara_v01_ch01_[Taruby]_v1.1.zip must be before [Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            // due to duplicate version identifiers in file.
            new Regex(
                @"(?<Series>.*)(v|s)\d+(-\d+)?(_|\s)",
                MatchOptions, RegexTimeout),
            //[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            new Regex(
                @"(?<Series>.*)(v|s)\d+(-\d+)?",
                MatchOptions, RegexTimeout),
            // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz
            new Regex(
                @"(?<Series>.*) (?<Chapter>\d+) (?:\(\d{4}\)) ",
                MatchOptions, RegexTimeout),
            // Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire)
            new Regex(
                @"(?<Series>.*) (?<Chapter>\d+(?:.\d+|-\d+)?) \(\d{4}\)",
                MatchOptions, RegexTimeout),
            // Noblesse - Episode 429 (74 Pages).7z
            new Regex(
                @"(?<Series>.*)(\s|_)(?:Episode|Ep\.?)(\s|_)(?<Chapter>\d+(?:.\d+|-\d+)?)",
                MatchOptions, RegexTimeout),
            // Akame ga KILL! ZERO (2016-2019) (Digital) (LuCaZ)
            new Regex(
                @"(?<Series>.*)\(\d",
                MatchOptions, RegexTimeout),
            // Tonikaku Kawaii (Ch 59-67) (Ongoing)
            new Regex(
                @"(?<Series>.*)(\s|_)\((c\s|ch\s|chapter\s)",
                MatchOptions, RegexTimeout),
            // Black Bullet (This is very loose, keep towards bottom)
            new Regex(
                @"(?<Series>.*)(_)(v|vo|c|volume)( |_)\d+",
                MatchOptions, RegexTimeout),
            // [Hidoi]_Amaenaideyo_MS_vol01_chp02.rar
            new Regex(
                @"(?<Series>.*)( |_)(vol\d+)?( |_)(?:Chp\.? ?\d+)",
                MatchOptions, RegexTimeout),
            // Mahoutsukai to Deshi no Futekisetsu na Kankei Chp. 1
            new Regex(
                @"(?<Series>.*)( |_)(?:Chp.? ?\d+)",
                MatchOptions, RegexTimeout),
            // Corpse Party -The Anthology- Sachikos game of love Hysteric Birthday 2U Chapter 01
            new Regex(
                @"^(?!Vol)(?<Series>.*)( |_)Chapter( |_)(\d+)",
                MatchOptions, RegexTimeout),

            // Fullmetal Alchemist chapters 101-108.cbz
            new Regex(
                @"^(?!vol)(?<Series>.*)( |_)(chapters( |_)?)\d+-?\d*",
                MatchOptions, RegexTimeout),
            // Umineko no Naku Koro ni - Episode 1 - Legend of the Golden Witch #1
            new Regex(
                @"^(?!Vol\.?)(?<Series>.*)( |_|-)(?<!-)(episode|chapter|(ch\.?) ?)\d+-?\d*",
                MatchOptions, RegexTimeout),

            // Baketeriya ch01-05.zip
            new Regex(
                @"^(?!Vol)(?<Series>.*)ch\d+-?\d?",
                MatchOptions, RegexTimeout),
            // Magi - Ch.252-005.cbz
            new Regex(
                @"(?<Series>.*)( ?- ?)Ch\.\d+-?\d*",
                MatchOptions, RegexTimeout),
            // [BAA]_Darker_than_Black_Omake-1.zip
            new Regex(
                @"^(?!Vol)(?<Series>.*)(-)\d+-?\d*", // This catches a lot of stuff ^(?!Vol)(?<Series>.*)( |_)(\d+)
                MatchOptions, RegexTimeout),
            // Kodoja #001 (March 2016)
            new Regex(
                @"(?<Series>.*)(\s|_|-)#",
                MatchOptions, RegexTimeout),
            // Baketeriya ch01-05.zip, Akiiro Bousou Biyori - 01.jpg, Beelzebub_172_RHS.zip, Cynthia the Mission 29.rar, A Compendium of Ghosts - 031 - The Third Story_ Part 12 (Digital) (Cobalt001)
            new Regex(
                @"^(?!Vol\.?)(?!Chapter)(?<Series>.+?)(\s|_|-)(?<!-)(ch|chapter)?\.?\d+-?\d*",
                MatchOptions, RegexTimeout),
            // [BAA]_Darker_than_Black_c1 (This is very greedy, make sure it's close to last)
            new Regex(
                @"^(?!Vol)(?<Series>.*)( |_|-)(ch?)\d+",
                MatchOptions, RegexTimeout),
        };

        private static readonly Regex[] ComicSeriesRegex = new[]
        {
            // Invincible Vol 01 Family matters (2005) (Digital)
            new Regex(
                @"(?<Series>.*)(\b|_)(vol\.?)( |_)(?<Volume>\d+(-\d+)?)",
                MatchOptions, RegexTimeout),
            // Batman Beyond 2.0 001 (2013)
            new Regex(
                @"^(?<Series>.+?\S\.\d) (?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // 04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)
            new Regex(
            @"^(?<Volume>\d+)\s(-\s|_)(?<Series>.*(\d{4})?)( |_)(\(|\d+)",
                MatchOptions, RegexTimeout),
            // 01 Spider-Man & Wolverine 01.cbr
            new Regex(
            @"^(?<Volume>\d+)\s(?:-\s)(?<Series>.*) (\d+)?",
                MatchOptions, RegexTimeout),
            // Batman & Wildcat (1 of 3)
            new Regex(
            @"(?<Series>.*(\d{4})?)( |_)(?:\((?<Volume>\d+) of \d+)",
                MatchOptions, RegexTimeout),
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: |_)v\d+",
                MatchOptions, RegexTimeout),
            // Amazing Man Comics chapter 25
            new Regex(
                @"^(?<Series>.*)(?: |_)c(hapter) \d+",
                MatchOptions, RegexTimeout),
            // Amazing Man Comics issue #25
            new Regex(
                @"^(?<Series>.*)(?: |_)i(ssue) #\d+",
                MatchOptions, RegexTimeout),
            // Batman Wayne Family Adventures - Ep. 001 - Moving In
            new Regex(
                @"^(?<Series>.+?)(\s|_|-)?(?:Ep\.?)(\s|_|-)+\d+",
                MatchOptions, RegexTimeout),
            // Batgirl Vol.2000 #57 (December, 2004)
            new Regex(
                @"^(?<Series>.+?)Vol\.?\s?#?(?:\d+)",
                MatchOptions, RegexTimeout),
            // Batman & Robin the Teen Wonder #0
            new Regex(
                @"^(?<Series>.*)(?: |_)#\d+",
                MatchOptions, RegexTimeout),
            // Batman & Catwoman - Trail of the Gun 01, Batman & Grendel (1996) 01 - Devil's Bones, Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.+?)(?: \d+)",
                MatchOptions, RegexTimeout),
            // Scott Pilgrim 02 - Scott Pilgrim vs. The World (2005)
            new Regex(
                @"^(?<Series>.*)(?: |_)(?<Volume>\d+)",
                MatchOptions, RegexTimeout),
            // The First Asterix Frieze (WebP by Doc MaKS)
            new Regex(
                @"^(?<Series>.*)(?: |_)(?!\(\d{4}|\d{4}-\d{2}\))\(",
                MatchOptions, RegexTimeout),
            // spawn-123, spawn-chapter-123 (from https://github.com/Girbons/comics-downloader)
            new Regex(
                @"^(?<Series>.+?)-(chapter-)?(?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // MUST BE LAST: Batman & Daredevil - King of New York
            new Regex(
                @"^(?<Series>.*)",
                MatchOptions, RegexTimeout),
        };

        private static readonly Regex[] ComicVolumeRegex = new[]
        {
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: |_)v(?<Volume>\d+)",
                MatchOptions, RegexTimeout),
            // Batgirl Vol.2000 #57 (December, 2004)
            new Regex(
                @"^(?<Series>.+?)(?:\s|_)vol\.?\s?(?<Volume>\d+)",
                MatchOptions, RegexTimeout),
        };

        private static readonly Regex[] ComicChapterRegex = new[]
        {
            // Batman & Wildcat (1 of 3)
            new Regex(
                @"(?<Series>.*(\d{4})?)( |_)(?:\((?<Chapter>\d+) of \d+)",
                MatchOptions, RegexTimeout),
            // Batman Beyond 04 (of 6) (1999)
            new Regex(
                @"(?<Series>.+?)(?<Chapter>\d+)(\s|_|-)?\(of",
                MatchOptions, RegexTimeout),
            // Batman Beyond 2.0 001 (2013)
            new Regex(
                @"^(?<Series>.+?\S\.\d) (?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.+?)(?: |_)v(?<Volume>\d+)(?: |_)(c? ?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)(c? ?)",
                MatchOptions, RegexTimeout),
            // Batman & Robin the Teen Wonder #0
            new Regex(
                @"^(?<Series>.+?)(?:\s|_)#(?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // Batman 2016 - Chapter 01, Batman 2016 - Issue 01, Batman 2016 - Issue #01
            new Regex(
                @"^(?<Series>.+?)((c(hapter)?)|issue)(_|\s)#?(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)",
                MatchOptions, RegexTimeout),
            // Invincible 070.5 - Invincible Returns 1 (2010) (digital) (Minutemen-InnerDemons).cbr
            new Regex(
                @"^(?<Series>.+?)(?:\s|_)(c? ?(chapter)?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)(c? ?)-",
                MatchOptions, RegexTimeout),
            // Batgirl Vol.2000 #57 (December, 2004)
            new Regex(
                @"^(?<Series>.+?)(?:vol\.?\d+)\s#(?<Chapter>\d+)",
                MatchOptions,
                RegexTimeout),
            // Batman & Catwoman - Trail of the Gun 01, Batman & Grendel (1996) 01 - Devil's Bones, Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.+?)(?: (?<Chapter>\d+))",
                MatchOptions, RegexTimeout),

            // Saga 001 (2012) (Digital) (Empire-Zone)
            new Regex(
                @"(?<Series>.+?)(?: |_)(c? ?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)\s\(\d{4}",
                MatchOptions, RegexTimeout),
            // Amazing Man Comics chapter 25
            new Regex(
                @"^(?!Vol)(?<Series>.+?)( |_)c(hapter)( |_)(?<Chapter>\d*)",
                MatchOptions, RegexTimeout),
            // Amazing Man Comics issue #25
            new Regex(
                @"^(?!Vol)(?<Series>.+?)( |_)i(ssue)( |_) #(?<Chapter>\d*)",
                MatchOptions, RegexTimeout),
            // spawn-123, spawn-chapter-123 (from https://github.com/Girbons/comics-downloader)
            new Regex(
                @"^(?<Series>.+?)-(chapter-)?(?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // Cyberpunk 2077 - Your Voice 01
            // new Regex(
            //     @"^(?<Series>.+?\s?-\s?(?:.+?))(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)$",
            //     MatchOptions,
            // RegexTimeout),
        };

        private static readonly Regex[] ReleaseGroupRegex = new[]
        {
            // [TrinityBAKumA Finella&anon], [BAA]_, [SlowManga&OverloadScans], [batoto]
            new Regex(@"(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
                MatchOptions, RegexTimeout),
            // (Shadowcat-Empire),
            // new Regex(@"(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
            //     MatchOptions),
        };

        private static readonly Regex[] MangaChapterRegex = new[]
        {
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip, ...c90.5-100.5
            new Regex(
                @"(\b|_)(c|ch)(\.?\s?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)",
                MatchOptions, RegexTimeout),
            // [Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            new Regex(
                @"v\d+\.(?<Chapter>\d+(?:.\d+|-\d+)?)",
                MatchOptions, RegexTimeout),
            // Umineko no Naku Koro ni - Episode 3 - Banquet of the Golden Witch #02.cbz (Rare case, if causes issue remove)
            new Regex(
                @"^(?<Series>.*)(?: |_)#(?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // Green Worldz - Chapter 027, Kimi no Koto ga Daidaidaidaidaisuki na 100-nin no Kanojo Chapter 11-10
            new Regex(
                @"^(?!Vol)(?<Series>.*)\s?(?<!vol\. )\sChapter\s(?<Chapter>\d+(?:\.?[\d-]+)?)",
                MatchOptions, RegexTimeout),
            // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz, Hinowa ga CRUSH! 018.5 (2019) (Digital) (LuCaZ).cbz
            new Regex(
                @"^(?!Vol)(?<Series>.+?)(?<!Vol)\.?\s(?<Chapter>\d+(?:.\d+|-\d+)?)(?:\s\(\d{4}\))?(\b|_|-)",
                MatchOptions, RegexTimeout),
            // Tower Of God S01 014 (CBT) (digital).cbz
            new Regex(
                @"(?<Series>.*)\sS(?<Volume>\d+)\s(?<Chapter>\d+(?:.\d+|-\d+)?)",
                MatchOptions, RegexTimeout),
            // Beelzebub_01_[Noodles].zip, Beelzebub_153b_RHS.zip
            new Regex(
                @"^((?!v|vo|vol|Volume).)*(\s|_)(?<Chapter>\.?\d+(?:.\d+|-\d+)?)(?<Part>b)?(\s|_|\[|\()",
                MatchOptions, RegexTimeout),
            // Yumekui-Merry_DKThias_Chapter21.zip
            new Regex(
                @"Chapter(?<Chapter>\d+(-\d+)?)", //(?:.\d+|-\d+)?
                MatchOptions, RegexTimeout),
            // [Hidoi]_Amaenaideyo_MS_vol01_chp02.rar
            new Regex(
                @"(?<Series>.*)(\s|_)(vol\d+)?(\s|_)Chp\.? ?(?<Chapter>\d+)",
                MatchOptions, RegexTimeout),
            // Vol 1 Chapter 2
            new Regex(
              @"(?<Volume>((vol|volume|v))?(\s|_)?\.?\d+)(\s|_)(Chp|Chapter)\.?(\s|_)?(?<Chapter>\d+)",
              MatchOptions, RegexTimeout),

        };
        private static readonly Regex[] MangaEditionRegex = {
            // Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz
            new Regex(
                @"(?<Edition>({|\(|\[).* Edition(}|\)|\]))",
                MatchOptions, RegexTimeout),
            // Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz
            new Regex(
                @"(\b|_)(?<Edition>Omnibus(( |_)?Edition)?)(\b|_)?",
                MatchOptions, RegexTimeout),
            // To Love Ru v01 Uncensored (Ch.001-007)
            new Regex(
                @"(\b|_)(?<Edition>Uncensored)(\b|_)",
                MatchOptions, RegexTimeout),
            // AKIRA - c003 (v01) [Full Color] [Darkhorse].cbz
            new Regex(
                @"(\b|_)(?<Edition>Full(?: |_)Color)(\b|_)?",
                MatchOptions, RegexTimeout),
        };

        private static readonly Regex[] CleanupRegex =
        {
            // (), {}, []
            new Regex(
                @"(?<Cleanup>(\{\}|\[\]|\(\)))",
                MatchOptions, RegexTimeout),
            // (Complete)
            new Regex(
                @"(?<Cleanup>(\{Complete\}|\[Complete\]|\(Complete\)))",
                MatchOptions, RegexTimeout),
            // Anything in parenthesis
            new Regex(
                @"\(.*\)",
                MatchOptions, RegexTimeout),
        };

        private static readonly Regex[] MangaSpecialRegex =
        {
            // All Keywords, does not account for checking if contains volume/chapter identification. Parser.Parse() will handle.
            new Regex(
                @"(?<Special>Specials?|OneShot|One\-Shot|Omake|Extra( Chapter)?|Art Collection|Side( |_)Stories|Bonus)",
                MatchOptions, RegexTimeout),
        };

        private static readonly Regex[] ComicSpecialRegex =
        {
            // All Keywords, does not account for checking if contains volume/chapter identification. Parser.Parse() will handle.
            new Regex(
                @"(?<Special>Specials?|OneShot|One\-Shot|Extra( Chapter)?|Book \d.+?|Compendium \d.+?|Omnibus \d.+?|[_\s\-]TPB[_\s\-]|FCBD \d.+?|Absolute \d.+?|Preview \d.+?|Art Collection|Side( |_)Stories|Bonus)",
                MatchOptions, RegexTimeout),
        };

        // If SP\d+ is in the filename, we force treat it as a special regardless if volume or chapter might have been found.
        private static readonly Regex SpecialMarkerRegex = new Regex(
            @"(?<Special>SP\d+)",
                MatchOptions, RegexTimeout
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
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            ParserInfo ret;

            if (IsEpub(filePath))
            {
                ret = new ParserInfo()
                {
                    Chapters = ParseChapter(fileName) ?? ParseComicChapter(fileName),
                    Series = ParseSeries(fileName) ?? ParseComicSeries(fileName),
                    Volumes = ParseVolume(fileName) ?? ParseComicVolume(fileName),
                    Filename = Path.GetFileName(filePath),
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
                    Filename = Path.GetFileName(filePath),
                    Format = ParseFormat(filePath),
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    FullFilePath = filePath
                };
            }

            if (IsImage(filePath) && IsCoverImage(filePath)) return null;

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
                ret.Series = CleanTitle(ret.Series.Replace(edition, ""), type is LibraryType.Comic);
                ret.Edition = edition;
            }

            var isSpecial = type == LibraryType.Comic ? ParseComicSpecial(fileName) : ParseMangaSpecial(fileName);
            // We must ensure that we can only parse a special out. As some files will have v20 c171-180+Omake and that
            // could cause a problem as Omake is a special term, but there is valid volume/chapter information.
            if (ret.Chapters == DefaultChapter && ret.Volumes == DefaultVolume && !string.IsNullOrEmpty(isSpecial))
            {
                ret.IsSpecial = true;
                ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
            }

            // If we are a special with marker, we need to ensure we use the correct series name. we can do this by falling back to Folder name
            if (HasSpecialMarker(fileName))
            {
                ret.IsSpecial = true;
                ret.Chapters = DefaultChapter;
                ret.Volumes = DefaultVolume;

                ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
            }

            if (string.IsNullOrEmpty(ret.Series))
            {
                ret.Series = CleanTitle(fileName, type is LibraryType.Comic);
            }

            // Pdfs may have .pdf in the series name, remove that
            if (IsPdf(filePath) && ret.Series.ToLower().EndsWith(".pdf"))
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
                }

                var series = ParseSeries(folder);

                if ((string.IsNullOrEmpty(series) && i == fallbackFolders.Count - 1))
                {
                    ret.Series = CleanTitle(folder, type is LibraryType.Comic);
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

        public static string ParseComicSpecial(string filePath)
        {
            foreach (var regex in ComicSpecialRegex)
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
                        return CleanTitle(match.Groups["Series"].Value, true);
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
                    var hasPart = match.Groups["Part"].Success;
                    return FormatValue(value, hasPart);
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
                    var hasPart = match.Groups["Part"].Success;
                    return FormatValue(value, hasPart);
                }
            }

            return DefaultVolume;
        }

        private static string FormatValue(string value, bool hasPart)
        {
            if (!value.Contains("-"))
            {
                return RemoveLeadingZeroes(hasPart ? AddChapterPart(value) : value);
            }

            var tokens = value.Split("-");
            var from = RemoveLeadingZeroes(tokens[0]);
            if (tokens.Length == 2)
            {
                var to = RemoveLeadingZeroes(hasPart ? AddChapterPart(tokens[1]) : tokens[1]);
                return $"{@from}-{to}";
            }

            return @from;
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
                    var hasPart = match.Groups["Part"].Success;

                    return FormatValue(value, hasPart);
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
                        var hasPart = match.Groups["Part"].Success;
                        return FormatValue(value, hasPart);
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
                        title = title.Replace(match.Value, string.Empty).Trim();
                    }
                }
            }

            // TODO: Since we have loops like this, think about using a method
            foreach (var regex in MangaEditionRegex)
            {
                var matches = regex.Matches(title);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        title = title.Replace(match.Value, string.Empty).Trim();
                    }
                }
            }

            return title;
        }

        private static string RemoveMangaSpecialTags(string title)
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

        private static string RemoveComicSpecialTags(string title)
        {
            foreach (var regex in ComicSpecialRegex)
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
        /// <example>
        /// Hippos_the_Great [Digital], -> Hippos the Great
        /// </example>
        /// </summary>
        /// <param name="title"></param>
        /// <param name="isComic"></param>
        /// <returns></returns>
        public static string CleanTitle(string title, bool isComic = false)
        {
            title = RemoveReleaseGroup(title);

            title = RemoveEditionTagHolders(title);

            title = isComic ? RemoveComicSpecialTags(title) : RemoveMangaSpecialTags(title);


            title = title.Replace("_", " ").Trim();
            if (title.EndsWith("-") || title.EndsWith(","))
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
            var num = int.Parse(number);
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
            return NormalizeRegex.Replace(name, string.Empty).ToLower();
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
