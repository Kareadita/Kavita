using System.Collections.Generic;
using System.Linq;
using API.Entities;

namespace API.Extensions
{
    public static class VolumeListExtensions
    {
        public static Volume FirstWithChapters(this IList<Volume> volumes, bool inBookSeries)
        {
            return inBookSeries
                ? volumes.FirstOrDefault(v => v.Chapters.Any())
                : volumes.FirstOrDefault(v => v.Chapters.Any() && (v.Number == 1));
        }
    }
}