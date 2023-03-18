using System.Collections.Generic;
using System.IO;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Parser;
using API.Services.Tasks;
using Kavita.Common;

namespace API.Data;

/// <summary>
/// Responsible for creating Series, Volume, Chapter, MangaFiles for use in <see cref="ScannerService"/>
/// </summary>
public static class DbFactory
{
    public static Library Library(string name, LibraryType type)
    {
        return new Library()
        {
            Name = name,
            Type = type,
            Series = new List<Series>(),
            Folders = new List<FolderPath>(),
            AppUsers = new List<AppUser>()
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

    public static CollectionTag CollectionTag(int id, string title, string? summary = null, bool promoted = false)
    {
        title = title.Trim();
        return new CollectionTag()
        {
            Id = id,
            NormalizedTitle = title.ToNormalized(),
            Title = title,
            Summary = summary?.Trim(),
            Promoted = promoted,
            SeriesMetadatas = new List<SeriesMetadata>()
        };
    }

    public static ReadingList ReadingList(string title, string? summary = null, bool promoted = false, AgeRating rating = AgeRating.Unknown)
    {
        title = title.Trim();
        return new ReadingList()
        {
            NormalizedTitle = title.ToNormalized(),
            Title = title,
            Summary = summary?.Trim(),
            Promoted = promoted,
            Items = new List<ReadingListItem>(),
            AgeRating = rating
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

    public static Genre Genre(string name)
    {
        return new Genre()
        {
            Title = name.Trim().SentenceCase(),
            NormalizedTitle = name.ToNormalized()
        };
    }

    public static Tag Tag(string name)
    {
        return new Tag()
        {
            Title = name.Trim().SentenceCase(),
            NormalizedTitle = name.ToNormalized()
        };
    }

    public static Person Person(string name, PersonRole role)
    {
        return new Person()
        {
            Name = name.Trim(),
            NormalizedName = name.ToNormalized(),
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
            LastModified = File.GetLastWriteTime(filePath),
            LastModifiedUtc = File.GetLastWriteTimeUtc(filePath),
        };
    }

    public static Device Device(string name)
    {
        return new Device()
        {
            Name = name,
        };
    }

    public static AppUser AppUser(string username, string email, SiteTheme defaultTheme)
    {
        return new AppUser()
        {
            UserName = username,
            Email = email,
            ApiKey = HashUtil.ApiKey(),
            UserPreferences = new AppUserPreferences
            {
                Theme = defaultTheme
            }
        };
    }
}
