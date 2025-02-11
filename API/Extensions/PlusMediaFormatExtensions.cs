using System;
using System.Collections.Generic;
using API.DTOs.Scrobbling;
using API.Entities.Enums;

namespace API.Extensions;

public static class PlusMediaFormatExtensions
{
    public static PlusMediaFormat ConvertToPlusMediaFormat(this LibraryType libraryType, MangaFormat? seriesFormat = null)
    {

        return libraryType switch
        {
            LibraryType.Manga => seriesFormat is MangaFormat.Epub ? PlusMediaFormat.LightNovel : PlusMediaFormat.Manga,
            LibraryType.Comic => PlusMediaFormat.Comic,
            LibraryType.LightNovel => PlusMediaFormat.LightNovel,
            LibraryType.Book => PlusMediaFormat.LightNovel,
            LibraryType.Image => PlusMediaFormat.Manga,
            LibraryType.ComicVine => PlusMediaFormat.Comic,
            _ => throw new ArgumentOutOfRangeException(nameof(libraryType), libraryType, null)
        };
    }

    public static IEnumerable<LibraryType> ConvertToLibraryTypes(this PlusMediaFormat plusMediaFormat)
    {
        return plusMediaFormat switch
        {
            PlusMediaFormat.Manga => [LibraryType.Manga, LibraryType.Image],
            PlusMediaFormat.Comic => [LibraryType.Comic, LibraryType.ComicVine],
            PlusMediaFormat.LightNovel => [LibraryType.LightNovel, LibraryType.Book, LibraryType.Manga],
            PlusMediaFormat.Book => [LibraryType.LightNovel, LibraryType.Book],
            _ => throw new ArgumentOutOfRangeException(nameof(plusMediaFormat), plusMediaFormat, null)
        };
    }

    public static IList<MangaFormat> GetMangaFormats(this PlusMediaFormat? mediaFormat)
    {
        return mediaFormat.HasValue ? mediaFormat.Value.GetMangaFormats() : [MangaFormat.Archive];
    }

    public static IList<MangaFormat> GetMangaFormats(this PlusMediaFormat mediaFormat)
    {
        return mediaFormat switch
        {
            PlusMediaFormat.Manga => [MangaFormat.Archive, MangaFormat.Image],
            PlusMediaFormat.Comic => [MangaFormat.Archive],
            PlusMediaFormat.LightNovel => [MangaFormat.Epub, MangaFormat.Pdf],
            PlusMediaFormat.Book => [MangaFormat.Epub, MangaFormat.Pdf],
            _ => [MangaFormat.Archive]
        };
    }


}
