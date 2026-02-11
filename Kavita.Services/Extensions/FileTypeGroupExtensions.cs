using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Services.Extensions;

public static class FileTypeGroupExtensions
{
    public static string GetRegex(this FileTypeGroup fileTypeGroup)
    {
        switch (fileTypeGroup)
        {
            case FileTypeGroup.Archive:
                return Scanner.Parser.ArchiveFileExtensions;
            case FileTypeGroup.Epub:
                return Scanner.Parser.EpubFileExtension;
            case FileTypeGroup.Pdf:
                return Scanner.Parser.PdfFileExtension;
            case FileTypeGroup.Images:
                return Scanner.Parser.ImageFileExtensions;
            default:
                throw new ArgumentOutOfRangeException(nameof(fileTypeGroup), fileTypeGroup, null);
        }
    }
}
