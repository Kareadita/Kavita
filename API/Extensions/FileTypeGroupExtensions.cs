using System;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;
using MimeTypes;

namespace API.Extensions;

public static class FileTypeGroupExtensions
{
    /// <summary>
    /// Gets the regular expression pattern for the specified FileTypeGroup.
    /// </summary>
    /// <param name="fileTypeGroup">The FileTypeGroup.</param>
    /// <returns>The regular expression pattern.</returns>
    public static string GetRegex(this FileTypeGroup fileTypeGroup)
    {
        switch (fileTypeGroup)
        {
            case FileTypeGroup.Archive:
                return Parser.ArchiveFileExtensions;
            case FileTypeGroup.Epub:
                return Parser.EpubFileExtension;
            case FileTypeGroup.Pdf:
                return Parser.PdfFileExtension;
            case FileTypeGroup.Images:
                return Parser.ImageFileExtensions;
            default:
                throw new ArgumentOutOfRangeException(nameof(fileTypeGroup), fileTypeGroup, null);
        }
    }

    /// <summary>
    /// Gets the MIME type for the specified file format. Extends original MimeTypeMap adding non supported extensions by the nuget.
    /// </summary>
    /// <param name="format">The file format.</param>
    /// <returns>The MIME type.</returns>
    public static string GetMimeType(this string format)
    {
        // Add jxl format
        format = format.ToLowerInvariant();
        if (format == ".jxl")
            return "image/jxl";
        return MimeTypeMap.GetMimeType(format);
    }
}
