using System.IO;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Extensions;

namespace Kavita.Services.Scanner;

public class BookParser(IDirectoryService directoryService, IBookService bookService, BasicParser basicParser) : DefaultParser(directoryService)
{
    public override ParserInfo Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata = true, ComicInfo comicInfo = null)
    {
        ParserInfo info;
        if (enableMetadata)
        {
            info = bookService.ParseInfo(filePath);
            if (info == null) return null;
        }
        else
        {
            var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
            info = new ParserInfo
            {
                Filename = Path.GetFileName(filePath),
                Format = MangaFormat.Epub,
                Title = Scanner.Parser.RemoveExtensionIfSupported(fileName)!,
                FullFilePath = Scanner.Parser.NormalizePath(filePath),
                Series = Scanner.Parser.ParseSeries(fileName, type),
                Chapters = Scanner.Parser.ParseChapter(fileName, type),
                Volumes = Scanner.Parser.ParseVolume(fileName, type),
            };
        }

        info.ComicInfo = comicInfo;

        // We need a special piece of code to override the Series IF there is a special marker in the filename for epub files
        if (info.IsSpecial && info.Volumes is "0" or "0.0" && info.ComicInfo.Series != info.Series)
        {
            info.Series = info.ComicInfo.Series;
        }

        // This catches when original library type is Manga/Comic and when parsing with non
        if (!Scanner.Parser.IsLooseLeafVolume(Scanner.Parser.ParseVolume(info.Series, type)))
        {
            var parsedVolumeFromTitle = Scanner.Parser.ParseVolume(info.Title, type);
            var parsedVolumeFromSeries = Scanner.Parser.ParseVolume(info.Series, type);

            var hasVolumeInTitle = !Scanner.Parser.IsLooseLeafVolume(parsedVolumeFromTitle);
            var hasVolumeInSeries = !Scanner.Parser.IsLooseLeafVolume(parsedVolumeFromSeries);

            if (string.IsNullOrEmpty(info.ComicInfo?.Volume) && hasVolumeInTitle && (hasVolumeInSeries || string.IsNullOrEmpty(info.Series)))
            {
                // NOTE: I'm not sure the comment is true. I've never seen this triggered
                // This is likely a light novel for which we can set series from parsed title
                info.Series = Scanner.Parser.ParseSeries(info.Title, type);
                info.Volumes = parsedVolumeFromTitle;
            }
            else
            {
                var info2 = basicParser.Parse(filePath, rootPath, libraryRoot, LibraryType.Book, enableMetadata, comicInfo);
                info.Merge(info2);

                if (hasVolumeInSeries && info2 != null && Scanner.Parser.IsLooseLeafVolume(Scanner.Parser.ParseVolume(info2.Series, type)))
                {
                    // Override the Series name so it groups appropriately
                    info.Series = info2.Series;
                }
            }
        }

        return string.IsNullOrEmpty(info.Series) ? null : info;
    }

    /// <summary>
    /// Only applicable for Epub files
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public override bool IsApplicable(string filePath, LibraryType type)
    {
        return Scanner.Parser.IsEpub(filePath);
    }
}
