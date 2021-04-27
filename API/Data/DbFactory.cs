using System.Collections.Generic;
using API.Entities;
using API.Parser;
using API.Services.Tasks;

namespace API.Data
{
    /// <summary>
    /// Responsible for creating Series, Volume, Chapter, MangaFiles for use in <see cref="ScannerService"/>
    /// </summary>
    public static class DbFactory
    {
        public static Series Series(string name)
        {
            // var name = infos.Count > 0 ? infos[0].Series : key; // NOTE: Why do I need this? When will i have no infos?
            return new ()
            {
                Name = name,
                OriginalName = name,
                LocalizedName = name,
                NormalizedName = Parser.Parser.Normalize(name),
                SortName = name,
                Summary = string.Empty,
                Volumes = new List<Volume>()
            };
        }

        public static Volume Volume(string volumeNumber)
        {
            return new Volume()
            {
                Name = volumeNumber,
                Number = (int) Parser.Parser.MinimumNumberFromRange(volumeNumber),
                Chapters = new List<Chapter>()
            };
        }
    }
}