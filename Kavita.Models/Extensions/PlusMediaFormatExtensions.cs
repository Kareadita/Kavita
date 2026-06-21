using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Interfaces;

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


    /// <summary>
    /// Derives the matched <see cref="MetadataProvider"/> implied by whichever primary id is already present on the
    /// entity, or <c>null</c> when it has not been matched to any provider yet.
    /// </summary>
    public static MetadataProvider? ResolveMatchedProvider(this IHasMetadataIds entity)
        => ResolveMatchedProvider(entity.MangaBakaId, entity.AniListId, entity.HardcoverId, entity.CbrId);

    /// <summary>
    /// Derives the matched <see cref="MetadataProvider"/> implied by whichever primary id is already present on the
    /// request, or <c>null</c> when no id is set yet.
    /// </summary>
    public static MetadataProvider? ResolveMatchedProvider(this PlusSeriesRequestDto data)
        => ResolveMatchedProvider(data.MangabakaId, data.AniListId, data.HardcoverId, data.CbrId);

    /// <summary>
    /// Maps whichever primary id is present to its provider. AniList ids resolve to MangaBaka going forward.
    /// A matched id currently takes precedence over the format default (and the future Library.MetadataProvider override);
    /// callers that want the format fallback should coalesce with <see cref="GetMetadataProvider"/>.
    /// </summary>
    private static MetadataProvider? ResolveMatchedProvider(long? mangabakaId, int? aniListId, int? hardcoverId, int? cbrId)
    {
        if (mangabakaId is > 0 || aniListId is > 0) return MetadataProvider.Mangabaka;
        if (hardcoverId is > 0) return MetadataProvider.Hardcover;
        if (cbrId is > 0) return MetadataProvider.ComicBookRoundup;

        return null;
    }


    public static MetadataProvider GetMetadataProvider(this PlusMediaFormat plusFormat, Library? library = null)
    {
        // TODO: If Library != null then we just take the hardcoded value
        var primaryProvider = MetadataProvider.Mangabaka;
        if (plusFormat is PlusMediaFormat.Comic)
        {
            primaryProvider = MetadataProvider.ComicBookRoundup;
        } else if (plusFormat is PlusMediaFormat.LightNovel)
        {
            primaryProvider = MetadataProvider.Mangabaka;
        } else if (plusFormat is PlusMediaFormat.Book)
        {
            primaryProvider = MetadataProvider.Hardcover;
        }

        return primaryProvider;
    }


}
