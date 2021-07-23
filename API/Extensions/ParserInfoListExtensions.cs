﻿using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Parser;

namespace API.Extensions
{
    public static class ParserInfoListExtensions
    {
        /// <summary>
        /// Selects distinct volume numbers by the "Volumes" key on the ParserInfo
        /// </summary>
        /// <param name="infos"></param>
        /// <returns></returns>
        public static IList<string> DistinctVolumes(this IList<ParserInfo> infos)
        {
            return infos.Select(p => p.Volumes).Distinct().ToList();
        }

        /// <summary>
        /// Checks if a list of ParserInfos has a given chapter or not. Lookup occurs on Range property. If a chapter is
        /// special, then the <see cref="ParserInfo.Filename"/> is matched, else the <see cref="ParserInfo.Chapters"/> field is checked.
        /// </summary>
        /// <param name="infos"></param>
        /// <param name="chapter"></param>
        /// <returns></returns>
        public static bool HasInfo(this IList<ParserInfo> infos, Chapter chapter)
        {
            return chapter.IsSpecial ? infos.Any(v => v.Filename == chapter.Range)
                                    : infos.Any(v => v.Chapters == chapter.Range);
        }

        /// <summary>
        /// Returns the MangaFormat that is common to all the files. Unknown if files are mixed (should never happen) or no infos
        /// </summary>
        /// <param name="infos"></param>
        /// <returns></returns>
        public static MangaFormat GetFormat(this IList<ParserInfo> infos)
        {
            if (infos.Count == 0) return MangaFormat.Unknown;
            return infos.DistinctBy(x => x.Format).First().Format;
        }
    }
}
