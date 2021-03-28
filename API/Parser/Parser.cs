using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using API.Entities.Enums;

namespace API.Parser
{
    public static class Parser
    {
        public static readonly string MangaFileExtensions = @"\.cbz|\.zip|\.rar|\.cbr|.tar.gz|.7zip";
        public static readonly string ImageFileExtensions = @"^(\.png|\.jpeg|\.jpg)";
        private static readonly string XmlRegexExtensions = @"\.xml";
        private static readonly Regex ImageRegex = new Regex(ImageFileExtensions, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MangaFileRegex = new Regex(MangaFileExtensions, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex XmlRegex = new Regex(XmlRegexExtensions, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        //?: is a non-capturing group in C#, else anything in () will be a group
        private static readonly Regex[] MangaVolumeRegex = new[]
        {
            // Dance in the Vampire Bund v16-17
            new Regex(
                @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d+)( |_)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip or Dance in the Vampire Bund v16-17
            new Regex(
                @"(?<Series>.*)(\b|_)(?!\[)v(?<Volume>\d+(-\d+)?)(?!\])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Kodomo no Jikan vol. 10
            new Regex(
                @"(?<Series>.*)(\b|_)(vol\.? ?)(?<Volume>\d+(-\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)
            new Regex(
                @"(vol\.? ?)(?<Volume>0*[1-9]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Tonikaku Cawaii [Volume 11].cbz
            new Regex(
                @"(volume )(?<Volume>0?[1-9]+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Tower Of God S01 014 (CBT) (digital).cbz
            new Regex(   
                @"(?<Series>.*)(\b|_|)(S(?<Volume>\d+))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
        };

        private static readonly Regex[] MangaSeriesRegex = new[]
        {
            // Ichiban_Ushiro_no_Daimaou_v04_ch34_[VISCANS].zip
            new Regex(
            @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d*)( |_)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Gokukoku no Brynhildr - c001-008 (v01) [TrinityBAKumA], Black Bullet - v4 c17 [batoto]
            new Regex(
                @"(?<Series>.*)( - )(?:v|vo|c)\d",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip, Killing Bites Vol. 0001 Ch. 0001 - Galactica Scanlations (gb)
            new Regex(
                @"(?<Series>.*) (\b|_|-)v",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Kedouin Makoto - Corpse Party Musume, Chapter 19 [Dametrans].zip
            new Regex(
                @"(?<Series>.*)(?:, Chapter )(?<Chapter>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            //Tonikaku Cawaii [Volume 11], Darling in the FranXX - Volume 01.cbz
            new Regex(
                @"(?<Series>.*)(?: _|-|\[|\() ?v",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            //Knights of Sidonia c000 (S2 LE BD Omake - BLAME!) [Habanero Scans]
            new Regex(
                @"(?<Series>.*)(\bc\d+\b)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            //Ichinensei_ni_Nacchattara_v01_ch01_[Taruby]_v1.1.zip must be before [Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            // due to duplicate version identifiers in file.
            new Regex(
                @"(?<Series>.*)(v|s)\d+(-\d+)?(_| )",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            //[Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            new Regex(
                @"(?<Series>.*)(v|s)\d+(-\d+)?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz
            new Regex(
                @"(?<Series>.*) (?<Chapter>\d+) (?:\(\d{4}\)) ", 
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Goblin Slayer - Brand New Day 006.5 (2019) (Digital) (danke-Empire)
            new Regex(
                @"(?<Series>.*) (?<Chapter>\d+(?:.\d+|-\d+)?) \(\d{4}\)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Akame ga KILL! ZERO (2016-2019) (Digital) (LuCaZ)
            new Regex(
                @"(?<Series>.*)\(\d",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Tonikaku Kawaii (Ch 59-67) (Ongoing)
            new Regex(
                @"(?<Series>.*)( |_)\((c |ch |chapter )",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Black Bullet (This is very loose, keep towards bottom) (?<Series>.*)(_)(v|vo|c|volume)
            new Regex(
                @"(?<Series>.*)(_)(v|vo|c|volume)( |_)\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Akiiro Bousou Biyori - 01.jpg, Beelzebub_172_RHS.zip, Cynthia the Mission 29.rar
            new Regex(
                @"^(?!Vol)(?<Series>.*)( |_)(\d+)", 
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // [BAA]_Darker_than_Black_c1 (This is very greedy, make sure it's close to last)
            new Regex(
                @"(?<Series>.*)( |_)(c)\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };
        
        private static readonly Regex[] ComicSeriesRegex = new[]
        {
            // 04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)
            new Regex(
            @"^(?<Volume>\d+) (- |_)?(?<Series>.*(\d{4})?)( |_)(\(|\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // 01 Spider-Man & Wolverine 01.cbr
            new Regex(
            @"^(?<Volume>\d+) (?:- )?(?<Series>.*) (\d+)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Wildcat (1 of 3)
            new Regex(
            @"(?<Series>.*(\d{4})?)( |_)(?:\(\d+ of \d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: |_)v\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Catwoman - Trail of the Gun 01, Batman & Grendel (1996) 01 - Devil's Bones, Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: \d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Robin the Teen Wonder #0
            new Regex(
                @"^(?<Series>.*)(?: |_)#\d+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Scott Pilgrim 02 - Scott Pilgrim vs. The World (2005)
            new Regex(
                @"^(?<Series>.*)(?: |_)(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // The First Asterix Frieze (WebP by Doc MaKS)
            new Regex(
                @"^(?<Series>.*)(?: |_)(?!\(\d{4}|\d{4}-\d{2}\))\(",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // MUST BE LAST: Batman & Daredevil - King of New York
            new Regex(
                @"^(?<Series>.*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };
        
        private static readonly Regex[] ComicVolumeRegex = new[]
        {
            // 04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)
            new Regex(
                @"^(?<Volume>\d+) (- |_)?(?<Series>.*(\d{4})?)( |_)(\(|\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // 01 Spider-Man & Wolverine 01.cbr
            new Regex(
                @"^(?<Volume>\d+) (?:- )?(?<Series>.*) (\d+)?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Wildcat (1 of 3)
            new Regex(
                @"(?<Series>.*(\d{4})?)( |_)(?:\((?<Chapter>\d+) of \d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: |_)v(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Scott Pilgrim 02 - Scott Pilgrim vs. The World (2005)
            new Regex(
                @"^(?<Series>.*)(?: |_)(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Catwoman - Trail of the Gun 01, Batman & Grendel (1996) 01 - Devil's Bones, Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: (?<Volume>\d+))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Robin the Teen Wonder #0
            new Regex(
                @"^(?<Series>.*)(?: |_)#(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };
        
        private static readonly Regex[] ComicChapterRegex = new[]
        {
            // 04 - Asterix the Gladiator (1964) (Digital-Empire) (WebP by Doc MaKS)
            new Regex(
                @"^(?<Volume>\d+) (- |_)?(?<Series>.*(\d{4})?)( |_)(\(|\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // 01 Spider-Man & Wolverine 01.cbr
            new Regex(
                @"^(?<Volume>\d+) (?:- )?(?<Series>.*) (\d+)?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Wildcat (1 of 3)
            new Regex(
                @"(?<Series>.*(\d{4})?)( |_)(?:\((?<Chapter>\d+) of \d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: |_)v(?<Volume>\d+)(?: |_)(c? ?)(?<Chapter>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Catwoman - Trail of the Gun 01, Batman & Grendel (1996) 01 - Devil's Bones, Teen Titans v1 001 (1966-02) (digital) (OkC.O.M.P.U.T.O.-Novus)
            new Regex(
                @"^(?<Series>.*)(?: (?<Volume>\d+))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Batman & Robin the Teen Wonder #0
            new Regex(
                @"^(?<Series>.*)(?: |_)#(?<Volume>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex[] ReleaseGroupRegex = new[]
        {
            // [TrinityBAKumA Finella&anon], [BAA]_, [SlowManga&OverloadScans], [batoto]
            new Regex(@"(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // (Shadowcat-Empire), 
            // new Regex(@"(?:\[(?<subgroup>(?!\s).+?(?<!\s))\](?:_|-|\s|\.)?)",
            //     RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex[] MangaChapterRegex = new[]
        {
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip, ...c90.5-100.5
            new Regex(
                @"(c|ch)(\.? ?)(?<Chapter>(\d+(\.\d)?)-?(\d+(\.\d)?)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // [Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            new Regex(
                @"v\d+\.(?<Chapter>\d+(?:.\d+|-\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Mob Psycho 100
            
            // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz, Hinowa ga CRUSH! 018.5 (2019) (Digital) (LuCaZ).cbz 
            new Regex(
                @"^(?!Vol)(?<Series>.*) (?<!vol\. )(?<Chapter>\d+(?:.\d+|-\d+)?)(?: \(\d{4}\))?", 
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Tower Of God S01 014 (CBT) (digital).cbz
            new Regex(
                @"(?<Series>.*) S(?<Volume>\d+) (?<Chapter>\d+(?:.\d+|-\d+)?)", 
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Beelzebub_01_[Noodles].zip
            new Regex(
                @"^((?!v|vo|vol|Volume).)*( |_)(?<Chapter>\.?\d+(?:.\d+|-\d+)?)( |_|\[|\()", 
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Yumekui-Merry_DKThias_Chapter21.zip
            new Regex(
                @"Chapter(?<Chapter>\d+(-\d+)?)", //(?:.\d+|-\d+)?
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
        };
        private static readonly Regex[] MangaEditionRegex = {
            //Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz
            new Regex(
                @"(?<Edition>({|\(|\[).* Edition(}|\)|\]))", 
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            //Tenjo Tenge {Full Contact Edition} v01 (2011) (Digital) (ASTC).cbz
            new Regex(
                @"(\b|_)(?<Edition>Omnibus)(\b|_)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        private static readonly Regex[] CleanupRegex =
        {
            // (), {}, []
            new Regex(
                @"(?<Cleanup>(\{\}|\[\]|\(\)))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // (Complete)
            new Regex(
                @"(?<Cleanup>(\{Complete\}|\[Complete\]|\(Complete\)))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Anything in parenthesis
            new Regex(
                @"\(.*\)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };


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
            var directoryName = (new FileInfo(filePath)).Directory?.Name;
            var rootName = (new DirectoryInfo(rootPath)).Name;
            
            var ret = new ParserInfo()
            {
                Chapters = type == LibraryType.Manga ? ParseChapter(fileName) : ParseComicChapter(fileName),
                Series = type == LibraryType.Manga ? ParseSeries(fileName) : ParseComicSeries(fileName),
                Volumes = type == LibraryType.Manga ? ParseVolume(fileName) : ParseComicVolume(fileName),
                Filename = fileName,
                Format = ParseFormat(filePath),
                FullFilePath = filePath
            };

            if (ret.Series == string.Empty && directoryName != null && directoryName != rootName)
            {
                ret.Series = ParseSeries(directoryName);
                if (ret.Series == string.Empty) ret.Series = CleanTitle(directoryName);
            }

            var edition = ParseEdition(fileName);
            if (!string.IsNullOrEmpty(edition))
            {
                ret.Series = CleanTitle(ret.Series.Replace(edition, ""));
                ret.Edition = edition;
            }
            

            return ret.Series == string.Empty ? null : ret;
        }

        private static MangaFormat ParseFormat(string filePath)
        {
            if (IsArchive(filePath)) return MangaFormat.Archive;
            if (IsImage(filePath)) return MangaFormat.Image;
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
                    if (match.Groups["Volume"] == Match.Empty) continue;
                    
                    var value = match.Groups["Volume"].Value;
                    if (!value.Contains("-")) return RemoveLeadingZeroes(match.Groups["Volume"].Value);
                    var tokens = value.Split("-");
                    var from = RemoveLeadingZeroes(tokens[0]);
                    var to = RemoveLeadingZeroes(tokens[1]);
                    return $"{@from}-{to}";

                }
            }
            
            return "0";
        }
        
        public static string ParseComicVolume(string filename)
        {
            foreach (var regex in ComicVolumeRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (match.Groups["Volume"] == Match.Empty) continue;
                    
                    var value = match.Groups["Volume"].Value;
                    if (!value.Contains("-")) return RemoveLeadingZeroes(match.Groups["Volume"].Value);
                    var tokens = value.Split("-");
                    var from = RemoveLeadingZeroes(tokens[0]);
                    var to = RemoveLeadingZeroes(tokens[1]);
                    return $"{@from}-{to}";

                }
            }
            
            return "0";
        }

        public static string ParseChapter(string filename)
        {
            foreach (var regex in MangaChapterRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (match.Groups["Chapter"] != Match.Empty)
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

            return "0";
        }
        
        public static string ParseComicChapter(string filename)
        {
            foreach (var regex in ComicChapterRegex)
            {
                var matches = regex.Matches(filename);
                foreach (Match match in matches)
                {
                    if (match.Groups["Chapter"] != Match.Empty)
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

            return "0";
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
                        title = title.Replace(match.Value, "");
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
            return MangaFileRegex.IsMatch(Path.GetExtension(filePath));
        }

        public static bool IsImage(string filePath)
        {
            if (filePath.StartsWith(".")) return false;
            return ImageRegex.IsMatch(Path.GetExtension(filePath));
        }
        
        public static bool IsXml(string filePath)
        {
            return XmlRegex.IsMatch(Path.GetExtension(filePath));
        }
        
        public static float MinimumNumberFromRange(string range)
        {
            var tokens = range.Split("-");
            return tokens.Min(float.Parse);
        }

        public static string Normalize(string name)
        {
            return name.ToLower().Replace("-", "").Replace(" ", "").Replace(":", "");
        }

        
    }
}