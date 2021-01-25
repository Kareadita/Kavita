using System;
using System.IO;
using System.Text.RegularExpressions;
using API.Entities;

namespace API.Parser
{
    public static class Parser
    {
        public static readonly string MangaFileExtensions = @"\.cbz|\.zip"; // |\.rar|\.cbr
        public static readonly string ImageFileExtensions = @"\.png|\.jpeg|\.jpg|\.gif";

        //?: is a non-capturing group in C#, else anything in () will be a group
        private static readonly Regex[] MangaVolumeRegex = new[]
        {
            // Dance in the Vampire Bund v16-17
            new Regex(
                @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d+)( |_)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Historys Strongest Disciple Kenichi_v11_c90-98.zip or Dance in the Vampire Bund v16-17
            new Regex(
                @"(?<Series>.*)(\b|_)v(?<Volume>\d+-?\d*)",
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
            // Kedouin Makoto - Corpse Party Musume, Chapter 19 [Dametrans].zip
            new Regex(
                @"(?<Series>.*)(?:, Chapter )(?<Chapter>\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Akame ga KILL! ZERO (2016-2019) (Digital) (LuCaZ)
            new Regex(
                @"(?<Series>.*)\(\d",
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
            new Regex(

                @"(c|ch)(\.? ?)(?<Chapter>\d+-?\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // [Suihei Kiki]_Kasumi_Otoko_no_Ko_[Taruby]_v1.1.zip
            new Regex(

                @"v\d+\.(?<Chapter>\d+-?\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Hinowa ga CRUSH! 018 (2019) (Digital) (LuCaZ).cbz
            new Regex(
                @"(?<Series>.*) (?<Chapter>\d+) (?:\(\d{4}\))", 
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Tower Of God S01 014 (CBT) (digital).cbz
            new Regex(
                @"(?<Series>.*) S(?<Volume>\d+) (?<Chapter>\d+)", 
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            // Beelzebub_01_[Noodles].zip
            new Regex(
                @"^((?!v|vo|vol|Volume).)*( |_)(?<Chapter>\.?\d+)( |_)", 
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
        /// <returns><see cref="ParserInfo"/> or null if Series was empty</returns>
        public static ParserInfo? Parse(string filePath, string rootPath)
        {
            var fileName = Path.GetFileName(filePath);
            var directoryName = (new FileInfo(filePath)).Directory?.Name;
            var rootName = (new DirectoryInfo(rootPath)).Name;
            
            var ret = new ParserInfo()
            {
                Chapters = ParseChapter(fileName),
                Series = ParseSeries(fileName),
                Volumes = ParseVolume(fileName),
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
            var fileInfo = new FileInfo(filePath);
            return MangaFileExtensions.Contains(fileInfo.Extension);
        }

        public static bool IsImage(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return ImageFileExtensions.Contains(fileInfo.Extension);
        }
    }
}