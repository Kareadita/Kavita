using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers.Builders;

namespace API.Tests.Helpers;

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
            NormalizedName = name.ToNormalized(),
            OriginalName = name,
            NormalizedLocalizedName = name.ToNormalized(),
            Volumes = new List<Volume>(),
            Metadata = new SeriesMetadata()
        };
    }

    public static Volume CreateVolume(string volumeNumber, List<Chapter> chapters = null)
    {
        var chaps = chapters ?? new List<Chapter>();
        var pages = chaps.Count > 0 ? chaps.Max(c => c.Pages) : 0;
        return new Volume()
        {
            Name = volumeNumber,
            Number = (int) API.Services.Tasks.Scanner.Parser.Parser.MinNumberFromRange(volumeNumber),
            Pages = pages,
            Chapters = chaps
        };
    }

    public static Chapter CreateChapter(string range, bool isSpecial, List<MangaFile> files = null, int pageCount = 0, string title = null)
    {
        return new Chapter()
        {
            IsSpecial = isSpecial,
            Range = range,
            Number = API.Services.Tasks.Scanner.Parser.Parser.MinNumberFromRange(range) + string.Empty,
            Files = files ?? new List<MangaFile>(),
            Pages = pageCount,
            Title = title ?? range
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



