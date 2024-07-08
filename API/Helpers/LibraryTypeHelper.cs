using System;
using API.DTOs.Scrobbling;
using API.Entities.Enums;

namespace API.Helpers;
#nullable enable

public static class LibraryTypeHelper
{
    public static MediaFormat GetFormat(LibraryType libraryType)
    {
        // TODO: Refactor this to an extension on LibraryType
        return libraryType switch
        {
            LibraryType.Manga => MediaFormat.Manga,
            LibraryType.Comic => MediaFormat.Comic,
            LibraryType.LightNovel => MediaFormat.LightNovel,
            LibraryType.Book => MediaFormat.LightNovel,
            LibraryType.Image => MediaFormat.Manga,
            LibraryType.ComicVine => MediaFormat.Comic,
            _ => throw new ArgumentOutOfRangeException(nameof(libraryType), libraryType, null)
        };
    }
}
