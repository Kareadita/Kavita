using System;
using API.DTOs.Scrobbling;
using API.Entities.Enums;

namespace API.Helpers;
#nullable enable

public static class LibraryTypeHelper
{
    public static MediaFormat GetFormat(LibraryType libraryType)
    {
        return libraryType switch
        {
            LibraryType.Manga => MediaFormat.Manga,
            LibraryType.Comic => MediaFormat.Comic,
            LibraryType.LightNovel => MediaFormat.LightNovel,
            _ => throw new ArgumentOutOfRangeException(nameof(libraryType), libraryType, null)
        };
    }
}
