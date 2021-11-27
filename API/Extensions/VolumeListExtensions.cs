using System.Collections.Generic;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Entities.Enums;

namespace API.Extensions
{
    public static class VolumeListExtensions
    {
        public static Volume FirstWithChapters(this IEnumerable<Volume> volumes, bool inBookSeries)
        {
            return inBookSeries
                ? volumes.FirstOrDefault(v => v.Chapters.Any())
                : volumes.OrderBy(v => v.Number, new ChapterSortComparer()).FirstOrDefault(v => v.Chapters.Any());
        }

        /// <summary>
        /// Selects the first Volume to get the cover image from. For a book with only a special, the special will be returned.
        /// If there are both specials and non-specials, then the first non-special will be returned.
        /// </summary>
        /// <param name="volumes"></param>
        /// <param name="seriesFormat"></param>
        /// <returns></returns>
        public static Volume GetCoverImage(this IList<Volume> volumes, MangaFormat seriesFormat)
        {
            if (seriesFormat is MangaFormat.Epub or MangaFormat.Pdf)
            {
                return volumes.OrderBy(x => x.Number).FirstOrDefault();
            }

            if (volumes.Any(x => x.Number != 0))
            {
                return volumes.OrderBy(x => x.Number).FirstOrDefault(x => x.Number != 0);
            }
            return volumes.OrderBy(x => x.Number).FirstOrDefault();
        }
    }
}
