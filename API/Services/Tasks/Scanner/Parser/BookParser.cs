using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;

public class BookParser(IDirectoryService directoryService, IBookService bookService, IDefaultParser basicParser) : DefaultParser(directoryService)
{
    public override ParserInfo Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, ComicInfo comicInfo = null)
    {
        var info = bookService.ParseInfo(filePath);
        if (info == null) return null;

        info.ComicInfo = comicInfo;

        // This catches when original library type is Manga/Comic and when parsing with non
        if (Parser.ParseVolume(info.Series) != Parser.LooseLeafVolume) // Shouldn't this be info.Volume != DefaultVolume?
        {
            var hasVolumeInTitle = !Parser.ParseVolume(info.Title)
                .Equals(Parser.LooseLeafVolume);
            var hasVolumeInSeries = !Parser.ParseVolume(info.Series)
                .Equals(Parser.LooseLeafVolume);

            if (string.IsNullOrEmpty(info.ComicInfo?.Volume) && hasVolumeInTitle && (hasVolumeInSeries || string.IsNullOrEmpty(info.Series)))
            {
                // This is likely a light novel for which we can set series from parsed title
                info.Series = Parser.ParseSeries(info.Title);
                info.Volumes = Parser.ParseVolume(info.Title);
            }
            else
            {
                var info2 = basicParser.Parse(filePath, rootPath, libraryRoot, LibraryType.Book, comicInfo);
                info.Merge(info2);
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
