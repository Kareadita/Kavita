using System;
using System.Collections.Generic;
using System.IO;
using API.Data.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Parser;
using API.Services.Tasks;

namespace API.Data;

/// <summary>
/// Responsible for creating Series, Volume, Chapter, MangaFiles for use in <see cref="ScannerService"/>
/// </summary>
public static class DbFactory
{
    public static Series Series(string name)
    {
        return new Series
        {
            Name = name,
            OriginalName = name,
            LocalizedName = name,
            NormalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(name),
            NormalizedLocalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(name),
            SortName = name,
            Volumes = new List<Volume>(),
            Metadata = SeriesMetadata(new List<CollectionTag>())
        };
    }

    public static Series Series(string name, string localizedName)
    {
        if (string.IsNullOrEmpty(localizedName))
        {
            localizedName = name;
        }
        return new Series
        {
            Name = name,
            OriginalName = name,
            LocalizedName = localizedName,
            NormalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(name),
            NormalizedLocalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(localizedName),
            SortName = name,
            Volumes = new List<Volume>(),
            Metadata = SeriesMetadata(new List<CollectionTag>())
        };
    }

    public static Volume Volume(string volumeNumber)
    {
        return new Volume()
        {
            Name = volumeNumber,
            Number = (int) Services.Tasks.Scanner.Parser.Parser.MinNumberFromRange(volumeNumber),
            Chapters = new List<Chapter>()
        };
    }

    public static Chapter Chapter(ParserInfo info)
    {
        var specialTreatment = info.IsSpecialInfo();
        var specialTitle = specialTreatment ? info.Filename : info.Chapters;
        return new Chapter()
        {
            Number = specialTreatment ? "0" : Services.Tasks.Scanner.Parser.Parser.MinNumberFromRange(info.Chapters) + string.Empty,
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
            CollectionTags = collectionTags,
            Summary = string.Empty
        };
    }

    public static CollectionTag CollectionTag(int id, string title, string summary, bool promoted)
    {
        return new CollectionTag()
        {
            Id = id,
            NormalizedTitle = Services.Tasks.Scanner.Parser.Parser.Normalize(title?.Trim()),
            Title = title?.Trim(),
            Summary = summary?.Trim(),
            Promoted = promoted,
            SeriesMetadatas = new List<SeriesMetadata>()
        };
    }

    public static ReadingList ReadingList(string title, string summary, bool promoted)
    {
        return new ReadingList()
        {
            NormalizedTitle = Services.Tasks.Scanner.Parser.Parser.Normalize(title?.Trim()),
            Title = title?.Trim(),
            Summary = summary?.Trim(),
            Promoted = promoted,
            Items = new List<ReadingListItem>()
        };
    }

    public static ReadingListItem ReadingListItem(int index, int seriesId, int volumeId, int chapterId)
    {
        return new ReadingListItem()
        {
            Order = index,
            ChapterId = chapterId,
            SeriesId = seriesId,
            VolumeId = volumeId
        };
    }

    public static Genre Genre(string name, bool external)
    {
        return new Genre()
        {
            Title = name.Trim().SentenceCase(),
            NormalizedTitle = Services.Tasks.Scanner.Parser.Parser.Normalize(name),
            ExternalTag = external
        };
    }

    public static Tag Tag(string name, bool external)
    {
        return new Tag()
        {
            Title = name.Trim().SentenceCase(),
            NormalizedTitle = Services.Tasks.Scanner.Parser.Parser.Normalize(name),
            ExternalTag = external
        };
    }

    public static Person Person(string name, PersonRole role)
    {
        return new Person()
        {
            Name = name.Trim(),
            NormalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(name),
            Role = role
        };
    }

    public static MangaFile MangaFile(string filePath, MangaFormat format, int pages)
    {
        return new MangaFile()
        {
            FilePath = filePath,
            Format = format,
            Pages = pages,
            LastModified = File.GetLastWriteTime(filePath)
        };
    }

    public static Device Device(string name)
    {
        return new Device()
        {
            Name = name,
        };
    }

}
