using Kavita.Models.Parser;

namespace Kavita.Services.Extensions;

public static class ParserExtensions
{

    extension(ParserInfo info)
    {
        /// <summary>
        /// Merges non-empty/null properties from info2 into this entity.
        /// </summary>
        /// <remarks>This does not merge ComicInfo as they should always be the same</remarks>
        /// <param name="info2"></param>
        public void Merge(ParserInfo? info2)
        {
            if (info2 == null) return;
            info.Chapters = Scanner.Parser.IsDefaultChapter(info.Chapters) ? info2.Chapters: info.Chapters;
            info.Volumes = Scanner.Parser.IsLooseLeafVolume(info.Volumes) ? info2.Volumes : info.Volumes;
            info.Edition = string.IsNullOrEmpty(info.Edition) ? info2.Edition : info.Edition;
            info.Title = string.IsNullOrEmpty(info.Title) ? info2.Title : info.Title;
            info.Series = string.IsNullOrEmpty(info.Series) ? info2.Series : info.Series;
            info.IsSpecial = info.IsSpecial || info2.IsSpecial;
        }

        /// <summary>
        /// If the ParserInfo has the IsSpecial tag or both volumes and chapters are default aka 0
        /// </summary>
        /// <returns></returns>
        public bool IsSpecialInfo()
        {
            return info.IsSpecial || (Scanner.Parser.IsLooseLeafVolume(info.Volumes) && Scanner.Parser.IsDefaultChapter(info.Chapters));
        }
    }

}
