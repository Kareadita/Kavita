using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;

public class BookParser(IDirectoryService directoryService, IBookService bookService, BasicParser basicParser) : DefaultParser(directoryService)
{
    public override ParserInfo Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, ComicInfo comicInfo = null)
    {
        var info = bookService.ParseInfo(filePath);
        if (info == null) return null;

        info.ComicInfo = comicInfo;

        // We need a special piece of code to override the Series IF there is a special marker in the filename for epub files
        if (info.IsSpecial && info.Volumes is "0" or "0.0" && info.ComicInfo.Series != info.Series)
        {
            info.Series = info.ComicInfo.Series;
        }

        // This catches when original library type is Manga/Comic and when parsing with non
        if (Parser.ParseVolume(info.Series, type) != Parser.LooseLeafVolume)
        {
            var hasVolumeInTitle = !Parser.ParseVolume(info.Title, type)
                .Equals(Parser.LooseLeafVolume);
            var hasVolumeInSeries = !Parser.ParseVolume(info.Series, type)
                .Equals(Parser.LooseLeafVolume);

            if (string.IsNullOrEmpty(info.ComicInfo?.Volume) && hasVolumeInTitle && (hasVolumeInSeries || string.IsNullOrEmpty(info.Series)))
            {
                // NOTE: I'm not sure the comment is true. I've never seen this triggered
                // This is likely a light novel for which we can set series from parsed title
                info.Series = Parser.ParseSeries(info.Title, type);
                info.Volumes = Parser.ParseVolume(info.Title, type);
            }
            else
            {
                var info2 = basicParser.Parse(filePath, rootPath, libraryRoot, LibraryType.Book, comicInfo);
                info.Merge(info2);
                if (hasVolumeInSeries && info2 != null && Parser.ParseVolume(info2.Series, type)
                        .Equals(Parser.LooseLeafVolume))
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
        return Parser.IsEpub(filePath);
    }
}
