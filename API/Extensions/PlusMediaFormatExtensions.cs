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

    public static IList<MangaFormat> GetMangaFormats(this PlusMediaFormat? mediaFormat)
    {
        if (mediaFormat == null) return [MangaFormat.Archive];
        return mediaFormat switch
        {
            PlusMediaFormat.Manga => [MangaFormat.Archive, MangaFormat.Image],
            PlusMediaFormat.Comic => [MangaFormat.Archive],
            PlusMediaFormat.LightNovel => [MangaFormat.Epub, MangaFormat.Pdf],
            PlusMediaFormat.Book => [MangaFormat.Epub, MangaFormat.Pdf],
            PlusMediaFormat.Unknown => [MangaFormat.Archive],
            _ => [MangaFormat.Archive]
        };
    }
}
