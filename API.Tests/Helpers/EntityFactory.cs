using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;

namespace API.Tests.Helpers
{
    /// <summary>
    /// Used to help quickly create DB entities for Unit Testing
    /// </summary>
    public static class EntityFactory
    {
        public static Series CreateSeries(string name)
        {
            return new Series()
            {
                Name = name,
                SortName = name,
                LocalizedName = name,
                NormalizedName = API.Parser.Parser.Normalize(name),
                Volumes = new List<Volume>()
            };
        }

        public static Volume CreateVolume(string volumeNumber, List<Chapter> chapters = null)
        {
            return new Volume()
            {
                Name = volumeNumber,
                Pages = 0,
                Chapters = chapters ?? new List<Chapter>()
            };
        }

        public static Chapter CreateChapter(string range, bool isSpecial, List<MangaFile> files = null)
        {
            return new Chapter()
            {
                IsSpecial = isSpecial,
                Range = range,
                Number = API.Parser.Parser.MinimumNumberFromRange(range) + string.Empty,
                Files = files ?? new List<MangaFile>(),
                Pages = 0,

            };
        }

        public static MangaFile CreateMangaFile(string filename, MangaFormat format, int pages)
        {
            return new MangaFile()
            {
                FilePath = filename,
                Format = format,
                Pages = pages
            };
        }
    }
}