using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;

public static partial class Parser
{
    public const string DefaultChapter = "0";
    public const string DefaultVolume = "0";
    private const int RegexTimeoutMs = 5000000; // 500 ms
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

    public const string ImageFileExtensions = @"^(\.png|\.jpeg|\.jpg|\.webp|\.gif)";
    public const string ArchiveFileExtensions = @"\.cbz|\.zip|\.rar|\.cbr|\.tar.gz|\.7zip|\.7z|\.cb7|\.cbt";
    private const string BookFileExtensions = @"\.epub|\.pdf";
    private const string XmlRegexExtensions = @"\.xml";
    public const string MacOsMetadataFileStartsWith = @"._";

    public const string SupportedExtensions =
        ArchiveFileExtensions + "|" + ImageFileExtensions + "|" + BookFileExtensions;

    private const RegexOptions MatchOptions =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly ImmutableArray<string> FormatTagSpecialKeywords = ImmutableArray.Create(
        "Special", "Reference", "Director's Cut", "Box Set", "Box-Set", "Annual", "Anthology", "Epilogue",
        "One Shot", "One-Shot", "Prologue", "TPB", "Trade Paper Back", "Omnibus", "Compendium", "Absolute", "Graphic Novel",
        "GN", "FCBD");

    private static readonly char[] LeadingZeroesTrimChars = new[] { '0' };

    private static readonly char[] SpacesAndSeparators = { '\0', '\t', '\r', ' ', '-', ','};


    private const string Number = @"\d+(\.\d)?";
    private const string NumberRange = Number + @"(-" + Number + @")?";

    /// <summary>
    /// non greedy matching of a string where parenthesis are balanced
    /// </summary>
    public const string BalancedParen = @"(?:[^()]|(?<open>\()|(?<-open>\)))*?(?(open)(?!))";
    /// <summary>
    /// non greedy matching of a string where square brackets are balanced
    /// </summary>
    public const string BalancedBracket = @"(?:[^\[\]]|(?<open>\[)|(?<-open>\]))*?(?(open)(?!))";
    /// <summary>
    /// Matches [Complete], release tags like [kmts] but not [ Complete ] or [kmts ]
    /// </summary>
    private const string TagsInBrackets = $@"\[(?!\s){BalancedBracket}(?<!\s)\]";
    /// <summary>
    /// Common regex patterns present in both Comics and Mangas
    /// </summary>
    private const string CommonSpecial = @"Specials?|One[- ]?Shot|Extra(?:\sChapter)?(?=\s)|Art Collection|Side Stories|Bonus";


    /// <summary>
    /// Matches against font-family css syntax. Does not match if url import has data: starting, as that is binary data
    /// </summary>
    /// <remarks>See here for some examples https://developer.mozilla.org/en-US/docs/Web/CSS/@font-face</remarks>
    [GeneratedRegex(@"(?<Start>(?:src:\s?)?(?:url|local)\((?!data:)" + "(?:[\"']?)" + @"(?!data:))"
                    + "(?<Filename>(?!data:)[^\"']+?)" + "(?<End>[\"']?" + @"\);?)", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    public static partial Regex FontSrcUrlRegex();

    /// <summary>
    /// https://developer.mozilla.org/en-US/docs/Web/CSS/@import
    /// </summary>
    [GeneratedRegex("(@import\\s([\"|']|url\\([\"|']))(?<Filename>[^'\"]+)([\"|']\\)?);", MatchOptions | RegexOptions.Multiline, matchTimeoutMilliseconds: RegexTimeoutMs)]
    public static partial Regex CssImportUrlRegex();

    /// <summary>
    /// Misc css image references, like background-image: url(), border-image, or list-style-image
    /// </summary>
    /// Original prepend: (background|border|list-style)-image:\s?)?
    [GeneratedRegex(@"(url\((?!data:).(?!data:))" + "(?<Filename>(?!data:)[^\"']*)" + @"(.\))", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    public static partial Regex CssImageUrlRegex();

    [GeneratedRegex(ImageFileExtensions, MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex ImageRegex();

    [GeneratedRegex(ArchiveFileExtensions, MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex ArchiveFileRegex();
    [GeneratedRegex(@"\.cbz|\.cbr|\.cb7|\.cbt", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex ComicInfoArchiveRegex();
    [GeneratedRegex(XmlRegexExtensions, MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex XmlRegex();
    [GeneratedRegex(BookFileExtensions, MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex BookFileRegex();

    [GeneratedRegex(@"(?<![[a-z]\d])(?:!?)(?<!back)(?<!back_)(?<!back-)(cover|folder)(?![\w\d])", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex CoverImageRegex();
    [GeneratedRegex(@"[^\p{L}0-9\+!]", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex NormalizeRegex();


    private static readonly Regex[] MangaVolumeRegex = new[]
    {
        // Dance in the Vampire Bund v16-17
        new Regex(
            @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d+)( |_)",
            MatchOptions, RegexTimeout),
        // NEEDLESS_Vol.4_-Simeon_6_v2[SugoiSugoi].rar
        new Regex(
            """(?<Series>.*)(\b|_)(?!\[)(vol\.?)(?<Volume>\d+(-\d+)?)(?!\])""",
            MatchOptions, RegexTimeout),
        // Historys Strongest Disciple Kenichi_v11_c90-98.zip or Dance in the Vampire Bund v16-17
        new Regex(
            @"(?<Series>.*)(\b|_)(?!\[)v(?<Volume>" + NumberRange + @")(?!\])",
            MatchOptions, RegexTimeout),
        // Kodomo no Jikan vol. 10, [dmntsf.net] One Piece - Digital Colored Comics Vol. 20.5-21.5 Ch. 177
        new Regex(
            """(?<Series>.*)(\b|_)(vol\.? ?)(?<Volume>\d+(\.\d)?(-\d+)?(\.\d)?)""",
            MatchOptions, RegexTimeout),
        // Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)
        new Regex(
            """(vol\.? ?)(?<Volume>\d+(\.\d)?)""",
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
        // Chinese Volume: 第n卷 -> Volume n, 第n册 -> Volume n, 幽游白书完全版 第03卷 天下 or 阿衰online 第1册
        new Regex(
            @"第(?<Volume>\d+)(卷|册)",
            MatchOptions, RegexTimeout),
        // Chinese Volume: 卷n -> Volume n, 册n -> Volume n
        new Regex(
            @"(卷|册)(?<Volume>\d+)",
            MatchOptions, RegexTimeout),
        // Korean Volume: 제n화|권|회|장 -> Volume n, n화|권|회|장 -> Volume n, 63권#200.zip -> Volume 63 (no chapter, #200 is just files inside)
        new Regex(
            @"제?(?<Volume>\d+(\.\d)?)(권|회|화|장)",
            MatchOptions, RegexTimeout),
        // Korean Season: 시즌n -> Season n,
        new Regex(
            @"시즌(?<Volume>\d+\-?\d+)",
            MatchOptions, RegexTimeout),
        // Korean Season: 시즌n -> Season n, n시즌 -> season n
        new Regex(
            @"(?<Volume>\d+(\-|~)?\d+?)시즌",
            MatchOptions, RegexTimeout),
        // Korean Season: 시즌n -> Season n, n시즌 -> season n
        new Regex(
            @"시즌(?<Volume>\d+(\-|~)?\d+?)",
            MatchOptions, RegexTimeout),
        // Japanese Volume: n巻 -> Volume n
        new Regex(
            @"(?<Volume>\d+(?:(\-)\d+)?)巻",
            MatchOptions, RegexTimeout),
        // Russian Volume: Том n -> Volume n, Тома n -> Volume
        new Regex(
            @"Том(а?)(\.?)(\s|_)?(?<Volume>\d+(?:(\-)\d+)?)",
            MatchOptions, RegexTimeout),
        // Russian Volume: n Том -> Volume n
        new Regex(
            @"(\s|_)?(?<Volume>\d+(?:(\-)\d+)?)(\s|_)Том(а?)",
            MatchOptions, RegexTimeout),
    };

    private static readonly Regex[] MangaSeriesRegex = new[]
    {
        // Russian Volume: Том n -> Volume n, Тома n -> Volume
        new Regex(
            @"(?<Series>.+?)Том(а?)(\.?)(\s|_)?(?<Volume>\d+(?:(\-)\d+)?)",
            MatchOptions, RegexTimeout),
        // Russian Volume: n Том -> Volume n
        new Regex(
            @"(?<Series>.+?)(\s|_)?(?<Volume>\d+(?:(\-)\d+)?)(\s|_)Том(а?)",
            MatchOptions, RegexTimeout),
        // Russian Chapter: n Главa -> Chapter n
        new Regex(
            @"(?<Series>.+?)(?!Том)(?<!Том\.)\s\d+(\s|_)?(?<Chapter>\d+(?:\.\d+|-\d+)?)(\s|_)(Глава|глава|Главы|Глава)",
            MatchOptions, RegexTimeout),
        // Russian Chapter: Главы n -> Chapter n
        new Regex(
            @"(?<Series>.+?)(Глава|глава|Главы|Глава)(\.?)(\s|_)?(?<Chapter>\d+(?:.\d+|-\d+)?)",
            MatchOptions, RegexTimeout),
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
            @"(?<Series>.*)( - )(?:v|vo|c|chapters)\d",
            MatchOptions, RegexTimeout),
        // Kedouin Makoto - Corpse Party Musume, Chapter 19 [Dametrans].zip
        new Regex(
            @"(?<Series>.*)(?:, Chapter )(?<Chapter>\d+)",
            MatchOptions, RegexTimeout),
        // Please Go Home, Akutsu-San! - Chapter 038.5 - Volume Announcement.cbz, My Charms Are Wasted on Kuroiwa Medaka - Ch. 37.5 - Volume Extras
        new Regex(
            @"(?<Series>.+?)(\s|_|-)(?!Vol)(\s|_|-)((?:Chapter)|(?:Ch\.))(\s|_|-)(?<Chapter>\d+)",
            MatchOptions, RegexTimeout),
        // [dmntsf.net] One Piece - Digital Colored Comics Vol. 20 Ch. 177 - 30 Million vs 81 Million.cbz
        new Regex(
            @"(?<Series>.+?):? (\b|_|-)(vol)\.?(\s|-|_)?\d+",
            MatchOptions, RegexTimeout),
        // [xPearse] Kyochuu Rettou Volume 1 [English] [Manga] [Volume Scans]
        new Regex(
            @"(?<Series>.+?):? (\b|_|-)(vol)(ume)",
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
            @"(?<Series>.*) (\b|_|-)(v|ch\.?|c|s)\d+",
            MatchOptions, RegexTimeout),
        // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz
        new Regex(
            @"(?<Series>.*)\s+(?<Chapter>\d+)\s+(?:\(\d{4}\))\s",
            MatchOptions, RegexTimeout),
        // Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire)
        new Regex(
            @"(?<Series>.*) (-)?(?<Chapter>\d+(?:.\d+|-\d+)?) \(\d{4}\)",
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
        // Fullmetal Alchemist chapters 101-108
        new Regex(
            @"(?<Series>.+?)(\s|_|\-)+?chapters(\s|_|\-)+?\d+(\s|_|\-)+?",
            MatchOptions, RegexTimeout),
        // It's Witching Time! 001 (Digital) (Anonymous1234)
        new Regex(
            @"(?<Series>.+?)(\s|_|\-)+?\d+(\s|_|\-)\(",
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
        // Japanese Volume: n巻 -> Volume n
        new Regex(
            @"(?<Series>.+?)第(?<Volume>\d+(?:(\-)\d+)?)巻",
            MatchOptions, RegexTimeout),

    };

    private static readonly Regex[] ComicSeriesRegex = new[]
    {
        // Russian Volume: Том n -> Volume n, Тома n -> Volume
        new Regex(
            @"(?<Series>.+?)Том(а?)(\.?)(\s|_)?(?<Volume>\d+(?:(\-)\d+)?)",
            MatchOptions, RegexTimeout),
        // Russian Volume: n Том -> Volume n
        new Regex(
            @"(?<Series>.+?)(\s|_)?(?<Volume>\d+(?:(\-)\d+)?)(\s|_)Том(а?)",
            MatchOptions, RegexTimeout),
        // Russian Chapter: n Главa -> Chapter n
        new Regex(
            @"(?<Series>.+?)(?!Том)(?<!Том\.)\s\d+(\s|_)?(?<Chapter>\d+(?:\.\d+|-\d+)?)(\s|_)(Глава|глава|Главы|Глава)",
            MatchOptions, RegexTimeout),
        // Russian Chapter: Главы n -> Chapter n
        new Regex(
            @"(?<Series>.+?)(Глава|глава|Главы|Глава)(\.?)(\s|_)?(?<Chapter>\d+(?:.\d+|-\d+)?)",
            MatchOptions, RegexTimeout),
        // Tintin - T22 Vol 714 pour Sydney
        new Regex(
            @"(?<Series>.+?)\s?(\b|_|-)\s?((vol|tome|t)\.?)(?<Volume>\d+(-\d+)?)",
            MatchOptions, RegexTimeout),
        // Invincible Vol 01 Family matters (2005) (Digital)
        new Regex(
            @"(?<Series>.+?)(\b|_)((vol|tome|t)\.?)(\s|_)(?<Volume>\d+(-\d+)?)",
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
        // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus), Aldebaran-Antares-t6
        new Regex(
            @"^(?<Series>.+?)(?: |_|-)(v|t)\d+",
            MatchOptions, RegexTimeout),
        // Amazing Man Comics chapter 25
        new Regex(
            @"^(?<Series>.+?)(?: |_)c(hapter) \d+",
            MatchOptions, RegexTimeout),
        // Amazing Man Comics issue #25
        new Regex(
            @"^(?<Series>.+?)(?: |_)i(ssue) #\d+",
            MatchOptions, RegexTimeout),
        // Batman Wayne Family Adventures - Ep. 001 - Moving In
        new Regex(
            @"^(?<Series>.+?)(\s|_|-)(?:Ep\.?)(\s|_|-)+\d+",
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
            @"^(?<Series>.+?)(?: |_)(?<Chapter>\d+)",
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
            @"^(?<Series>.+?)(?: |_)(t|v)(?<Volume>" + NumberRange + @")",
            MatchOptions, RegexTimeout),
        // Batgirl Vol.2000 #57 (December, 2004)
        new Regex(
            @"^(?<Series>.+?)(?:\s|_)(v|vol|tome|t)\.?(\s|_)?(?<Volume>\d+)",
            MatchOptions, RegexTimeout),
        // Chinese Volume: 第n卷 -> Volume n, 第n册 -> Volume n, 幽游白书完全版 第03卷 天下 or 阿衰online 第1册
        new Regex(
            @"第(?<Volume>\d+)(卷|册)",
            MatchOptions, RegexTimeout),
        // Chinese Volume: 卷n -> Volume n, 册n -> Volume n
        new Regex(
            @"(卷|册)(?<Volume>\d+)",
            MatchOptions, RegexTimeout),
        // Korean Volume: 제n권 -> Volume n, n권  -> Volume n, 63권#200.zip
        new Regex(
            @"제?(?<Volume>\d+)권",
            MatchOptions, RegexTimeout),
        // Japanese Volume: n巻 -> Volume n
        new Regex(
            @"(?<Volume>\d+(?:(\-)\d+)?)巻",
            MatchOptions, RegexTimeout),
        // Russian Volume: Том n -> Volume n, Тома n -> Volume
        new Regex(
            @"Том(а?)(\.?)(\s|_)?(?<Volume>\d+(?:(\-)\d+)?)",
            MatchOptions, RegexTimeout),
        // Russian Volume: n Том -> Volume n
        new Regex(
            @"(\s|_)?(?<Volume>\d+(?:(\-)\d+)?)(\s|_)Том(а?)",
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
        // Russian Chapter: Главы n -> Chapter n
        new Regex(
            @"(Глава|глава|Главы|Глава)(\.?)(\s|_)?(?<Chapter>\d+(?:.\d+|-\d+)?)",
            MatchOptions, RegexTimeout),
        // Russian Chapter: n Главa -> Chapter n
        new Regex(
            @"(?!Том)(?<!Том\.)\s\d+(\s|_)?(?<Chapter>\d+(?:\.\d+|-\d+)?)(\s|_)(Глава|глава|Главы|Глава)",
            MatchOptions, RegexTimeout),
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
    };

    private static readonly Regex[] MangaChapterRegex = new[]
    {
        // Historys Strongest Disciple Kenichi_v11_c90-98.zip, ...c90.5-100.5
        new Regex(
            @"(\b|_)(c|ch)(\.?\s?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)",
            MatchOptions, RegexTimeout),
        // [Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
        new Regex(
            @"v\d+\.(\s|_)(?<Chapter>\d+(?:.\d+|-\d+)?)",
            MatchOptions, RegexTimeout),
        // Umineko no Naku Koro ni - Episode 3 - Banquet of the Golden Witch #02.cbz (Rare case, if causes issue remove)
        new Regex(
            @"^(?<Series>.*)(?: |_)#(?<Chapter>\d+)",
            MatchOptions, RegexTimeout),
        // Green Worldz - Chapter 027, Kimi no Koto ga Daidaidaidaidaisuki na 100-nin no Kanojo Chapter 11-10
        new Regex(
            @"^(?!Vol)(?<Series>.*)\s?(?<!vol\. )\sChapter\s(?<Chapter>\d+(?:\.?[\d-]+)?)",
            MatchOptions, RegexTimeout),
        // Russian Chapter: Главы n -> Chapter n
        new Regex(
            @"(Глава|глава|Главы|Глава)(\.?)(\s|_)?(?<Chapter>\d+(?:.\d+|-\d+)?)",
            MatchOptions, RegexTimeout),
        // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz, Hinowa ga CRUSH! 018.5 (2019) (Digital) (LuCaZ).cbz
        new Regex(
            @"^(?!Vol)(?<Series>.+?)(?<!Vol)(?<!Vol.)\s(\d\s)?(?<Chapter>\d+(?:\.\d+|-\d+)?)(?:\s\(\d{4}\))?(\b|_|-)",
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
        // Chinese Chapter: 第n话 -> Chapter n, 【TFO汉化&Petit汉化】迷你偶像漫画第25话
        new Regex(
            @"第(?<Chapter>\d+)话",
            MatchOptions, RegexTimeout),
        // Korean Chapter: 제n화 -> Chapter n, 가디언즈 오브 갤럭시 죽음의 보석.E0008.7화#44
        new Regex(
            @"제?(?<Chapter>\d+\.?\d+)(회|화|장)",
            MatchOptions, RegexTimeout),
        // Korean Chapter: 第10話 -> Chapter n, [ハレム]ナナとカオル ～高校生のSMごっこ～　第1話
        new Regex(
            @"第?(?<Chapter>\d+(?:\.\d+|-\d+)?)話",
            MatchOptions, RegexTimeout),
        // Russian Chapter: n Главa -> Chapter n
        new Regex(
            @"(?!Том)(?<!Том\.)\s\d+(\s|_)?(?<Chapter>\d+(?:\.\d+|-\d+)?)(\s|_)(Глава|глава|Главы|Глава)",
            MatchOptions, RegexTimeout),
    };


    // Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz
    // To Love Ru v01 Uncensored (Ch.001-007)
    [GeneratedRegex(@"\b(?:Omnibus(?:\s?Edition)?|Uncensored)\b", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex MangaEditionRegex();

    /// <summary>
    /// Matches anything between balanced parenthesis, tags between brackets, {} and {Complete}
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex($@"(?:\({BalancedParen}\)|{TagsInBrackets}|\{{\}}|\{{Complete\}})", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex CleanupRegex();

    /// <summary>
    /// All Keywords, does not account for checking if contains volume/chapter identification. Parser.Parse() will handle.
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex($@"\b(?:{CommonSpecial}|Omake)\b", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex MangaSpecialRegex();

    /// <summary>
    /// All Keywords, does not account for checking if contains volume/chapter identification. Parser.Parse() will handle.
    /// </summary>
    [GeneratedRegex($@"\b(?:{CommonSpecial}|\d.+?(\W|-|^)Annual|Annual(\W|-|$)|Book \d.+?|Compendium(\W|-|$|\s.+?)|Omnibus(\W|-|$|\s.+?)|FCBD \d.+?|Absolute(\W|-|$|\s.+?)|Preview(\W|-|$|\s.+?)|Hors[ -]S[ée]rie|TPB|HS|THS)\b", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex ComicSpecialRegex();

    /// <summary>
    /// All Keywords, does not account for checking if contains volume/chapter identification. Parser.Parse() will handle.
    /// </summary>
    [GeneratedRegex(@"\b(?:Bd[-\s]Fr)\b", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex EuropeanComicRegex();

    /// <summary>
    /// Recognizes the Special token only
    /// </summary>
    /// <remarks>If SP\d+ is in the filename, we force treat it as a special regardless if volume or chapter might have been found.</remarks>
    /// <returns></returns>
    [GeneratedRegex(@"SP\d+", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex SpecialMarkerRegex();


    [GeneratedRegex(@"\s{2,}", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex EmptySpaceRegex();

    [GeneratedRegex(@"^[\d\-.]+$", MatchOptions, matchTimeoutMilliseconds: RegexTimeoutMs)]
    private static partial Regex NumberRangeRegex();



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
        filePath = ReplaceUnderscores(filePath);
        var match = MangaEditionRegex().Match(filePath);
        return match.Success ? match.Value : string.Empty;
    }

    /// <summary>
    /// If the file has SP marker.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static bool HasSpecialMarker(string filePath)
    {
        return SpecialMarkerRegex().IsMatch(filePath);
    }

    public static bool IsMangaSpecial(string filePath)
    {
        filePath = ReplaceUnderscores(filePath);
        return  MangaSpecialRegex().IsMatch(filePath);
    }

    public static bool IsComicSpecial(string filePath)
    {
        filePath = ReplaceUnderscores(filePath);
        return ComicSpecialRegex().IsMatch(filePath);
    }

    public static string ParseSeries(string filename)
    {
        foreach (var regex in MangaSeriesRegex)
        {
            var matches = regex.Matches(filename);
            var group = matches
                .Select(match => match.Groups["Series"])
                .FirstOrDefault(group => group.Success && group != Match.Empty);
            if (group != null) return CleanTitle(group.Value);
        }

        return string.Empty;
    }
    public static string ParseComicSeries(string filename)
    {
        foreach (var regex in ComicSeriesRegex)
        {
            var matches = regex.Matches(filename);
            var group = matches
                .Select(match => match.Groups["Series"])
                .FirstOrDefault(group => group.Success && group != Match.Empty);
            if (group != null) return CleanTitle(group.Value, true);
        }

        return string.Empty;
    }

    public static string ParseVolume(string filename)
    {
        foreach (var regex in MangaVolumeRegex)
        {
            var matches = regex.Matches(filename);
            foreach (var group in matches.Select(match => match.Groups))
            {
                if (!group["Volume"].Success || group["Volume"] == Match.Empty) continue;

                var value = group["Volume"].Value;
                var hasPart = group["Part"].Success;
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
            foreach (var group in matches.Select(match => match.Groups))
            {
                if (!group["Volume"].Success || group["Volume"] == Match.Empty) continue;

                var value = group["Volume"].Value;
                var hasPart = group["Part"].Success;
                return FormatValue(value, hasPart);
            }
        }

        return DefaultVolume;
    }

    private static string FormatValue(string value, bool hasPart)
    {
        if (!value.Contains('-'))
        {
            return RemoveLeadingZeroes(hasPart ? AddChapterPart(value) : value);
        }

        var tokens = value.Split("-");
        var from = RemoveLeadingZeroes(tokens[0]);
        if (tokens.Length != 2) return from;

        var to = RemoveLeadingZeroes(hasPart ? AddChapterPart(tokens[1]) : tokens[1]);
        return $"{from}-{to}";
    }

    public static string ParseChapter(string filename)
    {
        foreach (var regex in MangaChapterRegex)
        {
            var matches = regex.Matches(filename);
            foreach (var groups in matches.Select(match => match.Groups))
            {
                if (!groups["Chapter"].Success || groups["Chapter"] == Match.Empty) continue;

                var value = groups["Chapter"].Value;
                var hasPart = groups["Part"].Success;

                return FormatValue(value, hasPart);
            }
        }

        return DefaultChapter;
    }

    private static string AddChapterPart(string value)
    {
        if (value.Contains('.'))
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
            foreach (var groups in matches.Select(match => match.Groups))
            {
                if (!groups["Chapter"].Success || groups["Chapter"] == Match.Empty) continue;
                var value = groups["Chapter"].Value;
                var hasPart = groups["Part"].Success;
                return FormatValue(value, hasPart);

            }
        }

        return DefaultChapter;
    }

    private static string RemoveEditionTagHolders(string title)
    {
        title = CleanupRegex().Replace(title, string.Empty);

        title = MangaEditionRegex().Replace(title, string.Empty);

        return title;
    }

    private static string RemoveMangaSpecialTags(string title)
    {
        return MangaSpecialRegex().Replace(title, string.Empty);
    }

    private static string RemoveEuropeanTags(string title)
    {
        return EuropeanComicRegex().Replace(title, string.Empty);
    }

    private static string RemoveComicSpecialTags(string title)
    {
        return ComicSpecialRegex().Replace(title, string.Empty);
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

        title = ReplaceUnderscores(title);

        title = RemoveEditionTagHolders(title);

        if (isComic)
        {
            title = RemoveComicSpecialTags(title);
            title = RemoveEuropeanTags(title);
        }
        else
        {
            title = RemoveMangaSpecialTags(title);
        }

        title = title.Trim(SpacesAndSeparators);

        title = EmptySpaceRegex().Replace(title, " ");

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
        if (!number.Contains('-')) return PerformPadding(number);

        var tokens = number.Split("-");
        return $"{PerformPadding(tokens[0])}-{PerformPadding(tokens[1])}";
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
        var ret = title.TrimStart(LeadingZeroesTrimChars);
        return string.IsNullOrEmpty(ret) ? "0" : ret;
    }

    public static bool IsArchive(string filePath)
    {
        return ArchiveFileRegex().IsMatch(Path.GetExtension(filePath));
    }
    public static bool IsComicInfoExtension(string filePath)
    {
        return ComicInfoArchiveRegex().IsMatch(Path.GetExtension(filePath));
    }
    public static bool IsBook(string filePath)
    {
        return BookFileRegex().IsMatch(Path.GetExtension(filePath));
    }

    public static bool IsImage(string filePath)
    {
        return !filePath.StartsWith('.') && ImageRegex().IsMatch(Path.GetExtension(filePath));
    }

    public static bool IsXml(string filePath)
    {
        return XmlRegex().IsMatch(Path.GetExtension(filePath));
    }


    public static float MinNumberFromRange(string range)
    {
        try
        {
            if (!NumberRangeRegex().IsMatch(range))
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

    public static float MaxNumberFromRange(string range)
    {
        try
        {
            if (!NumberRangeRegex().IsMatch(range))
            {
                return (float) 0.0;
            }

            var tokens = range.Replace("_", string.Empty).Split("-");
            return tokens.Max(float.Parse);
        }
        catch
        {
            return (float) 0.0;
        }
    }

    public static string Normalize(string name)
    {
        return NormalizeRegex().Replace(name, string.Empty).ToLower();
    }

    /// <summary>
    /// Responsible for preparing special title for rendering to the UI. Replaces _ with ' ' and strips out SP\d+
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string CleanSpecialTitle(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var cleaned = SpecialMarkerRegex().Replace(name.Replace('_', ' '), string.Empty).Trim();
        var lastIndex = cleaned.LastIndexOf('.');
        if (lastIndex > 0)
        {
            cleaned = cleaned.Substring(0, cleaned.LastIndexOf('.')).Trim();
        }

        return string.IsNullOrEmpty(cleaned) ? name : cleaned;
    }


    /// <summary>
    /// Tests whether the file is a cover image such that: contains "cover", is named "folder", and is an image
    /// </summary>
    /// <remarks>If the path has "backcover" in it, it will be ignored</remarks>
    /// <param name="filename">Filename with extension</param>
    /// <returns></returns>
    public static bool IsCoverImage(string filename)
    {
        return IsImage(filename) && CoverImageRegex().IsMatch(filename);
    }

    /// <summary>
    /// Validates that a Path doesn't start with certain blacklisted folders, like __MACOSX, @Recently-Snapshot, etc and that if a full path, the filename
    /// doesn't start with ._, which is a metadata file on MACOSX.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool HasBlacklistedFolderInPath(string path)
    {
        return path.Contains("__MACOSX") || path.StartsWith("@Recently-Snapshot") || path.StartsWith("@recycle") || path.StartsWith("._") || Path.GetFileName(path).StartsWith("._") || path.Contains(".qpkg");
    }


    public static bool IsEpub(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".epub", StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool IsPdf(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".pdf", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Cleans an author's name
    /// </summary>
    /// <remarks>If the author is Last, First, this will not reverse</remarks>
    /// <param name="author"></param>
    /// <returns></returns>
    public static string CleanAuthor(string author)
    {
        return string.IsNullOrEmpty(author) ? string.Empty : author.Trim();
    }

    /// <summary>
    /// Cleans user query string input
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public static string CleanQuery(string query)
    {
        return Uri.UnescapeDataString(query).Trim().Replace(@"%", string.Empty)
            .Replace(":", string.Empty);
    }

    /// <summary>
    /// Normalizes the slashes in a path to be <see cref="Path.AltDirectorySeparatorChar"/>
    /// </summary>
    /// <example>/manga/1\1 -> /manga/1/1</example>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(@"//", Path.AltDirectorySeparatorChar + string.Empty);
    }

    /// <summary>
    /// Checks against a set of strings to validate if a ComicInfo.Format should receive special treatment
    /// </summary>
    /// <param name="comicInfoFormat"></param>
    /// <returns></returns>
    public static bool HasComicInfoSpecial(string comicInfoFormat)
    {
        return FormatTagSpecialKeywords.Contains(comicInfoFormat);
    }

    private static string? ReplaceUnderscores(string? name)
    {
        return string.IsNullOrEmpty(name) ? name : name.Replace('_', ' ');
    }
}
