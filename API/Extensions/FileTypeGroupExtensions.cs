using System;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;
using MimeTypes;

namespace API.Extensions;

public static class FileTypeGroupExtensions
{
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
    public static string GetMimeType(this string format)
    {
        //Add jxl format
        format = format.ToLowerInvariant();
        if (format == ".jxl")
            return "image/jxl";
        return MimeTypeMap.GetMimeType(format);
    }

}
