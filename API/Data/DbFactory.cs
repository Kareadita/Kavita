using System;
using System.Collections.Generic;
using API.Data.Metadata;
using API.Entities;
using API.Entities.Enums;
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
            return new ()
            {
                Name = name,
                OriginalName = name,
                LocalizedName = name,
                NormalizedName = Parser.Parser.Normalize(name),
                SortName = name,
                Summary = string.Empty,
                Volumes = new List<Volume>(),
                Metadata = SeriesMetadata(Array.Empty<CollectionTag>())
            };
        }

        public static SeriesMetadata SeriesMetadata(ComicInfo info)
        {
            return SeriesMetadata(Array.Empty<CollectionTag>());
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

        public static Chapter Chapter(ParserInfo info)
        {
            var specialTreatment = info.IsSpecialInfo();
            var specialTitle = specialTreatment ? info.Filename : info.Chapters;
            return new Chapter()
            {
                Number = specialTreatment ? "0" : Parser.Parser.MinimumNumberFromRange(info.Chapters) + string.Empty,
                Range = specialTreatment ? info.Filename : info.Chapters,
                Title = (specialTreatment && info.Format == MangaFormat.Epub)
                    ? info.Title
                    : specialTitle,
                Files = new List<MangaFile>(),
                IsSpecial = specialTreatment,
            };
        }

        public static SeriesMetadata SeriesMetadata(ICollection<CollectionTag> collectionTags)
        {
            return new SeriesMetadata()
            {
                CollectionTags = collectionTags
            };
        }

        public static CollectionTag CollectionTag(int id, string title, string summary, bool promoted)
        {
            return new CollectionTag()
            {
                Id = id,
                NormalizedTitle = API.Parser.Parser.Normalize(title?.Trim()).ToUpper(),
                Title = title?.Trim(),
                Summary = summary?.Trim(),
                Promoted = promoted
            };
        }
    }
}
