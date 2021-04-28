using System.IO;
using API.Entities.Enums;
using API.Parser;

namespace API.Tests.Helpers
{
    public static class ParserInfoFactory
    {
        public static ParserInfo CreateParsedInfo(string series, string volumes, string chapters, string filename, bool isSpecial)
        {
            return new ParserInfo()
            {
                Chapters = chapters,
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = Path.Join(@"/manga/", filename),
                Filename = filename,
                IsSpecial = isSpecial,
                Title = Path.GetFileNameWithoutExtension(filename),
                Series = series,
                Volumes = volumes
            };
        }
    }
}