using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.Extensions;
#nullable enable

public static class PlusMediaFormatExtensions
{
    public static PlusMediaFormat ConvertToPlusMediaFormat(this LibraryType libraryType, MangaFormat? seriesFormat = null)
    {
        // TODO: Amelia, let's rework this with v3/scrobbling
        return libraryType switch
        {
            LibraryType.Manga => seriesFormat is MangaFormat.Epub ? PlusMediaFormat.LightNovel : PlusMediaFormat.Manga,
            LibraryType.Comic => PlusMediaFormat.Comic,
            LibraryType.LightNovel => PlusMediaFormat.LightNovel,
            LibraryType.Book => PlusMediaFormat.Book,
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


    public static MetadataProvider GetMetadataProvider(this PlusMediaFormat plusFormat, Library? library = null)
    {
        // TODO: If Library != null then we just take the hardcoded value
        var primaryProvider = Models.Entities.Enums.MetadataProvider.Mangabaka;
        if (plusFormat is PlusMediaFormat.Comic)
        {
            primaryProvider = Models.Entities.Enums.MetadataProvider.ComicBookRoundup;
        } else if (plusFormat is PlusMediaFormat.LightNovel)
        {
            primaryProvider = Models.Entities.Enums.MetadataProvider.Mangabaka;
        } else if (plusFormat is PlusMediaFormat.Book)
        {
            primaryProvider = Models.Entities.Enums.MetadataProvider.Hardcover;
        }

        return primaryProvider;
    }


}
