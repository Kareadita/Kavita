using System;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;

namespace API.Services;

public interface IReadingItemService
{
    ComicInfo? GetComicInfo(string filePath);
    int GetNumberOfPages(string filePath, MangaFormat format);
    string GetCoverImage(string filePath, string fileName, MangaFormat format, bool saveAsWebP);
    void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1);
    ParserInfo? ParseFile(string path, string rootPath, LibraryType type);
}

public class ReadingItemService : IReadingItemService
{
    private readonly IArchiveService _archiveService;
    private readonly IBookService _bookService;
    private readonly IImageService _imageService;
    private readonly IDirectoryService _directoryService;
    private readonly IDefaultParser _defaultParser;

    public ReadingItemService(IArchiveService archiveService, IBookService bookService, IImageService imageService, IDirectoryService directoryService)
    {
        _archiveService = archiveService;
        _bookService = bookService;
        _imageService = imageService;
        _directoryService = directoryService;

        _defaultParser = new DefaultParser(directoryService);
    }

    /// <summary>
    /// Gets the ComicInfo for the file if it exists. Null otherwise.
    /// </summary>
    /// <param name="filePath">Fully qualified path of file</param>
    /// <returns></returns>
    public ComicInfo? GetComicInfo(string filePath)
    {
        if (Tasks.Scanner.Parser.Parser.IsEpub(filePath))
        {
            return _bookService.GetComicInfo(filePath);
        }

        if (Tasks.Scanner.Parser.Parser.IsComicInfoExtension(filePath))
        {
            return _archiveService.GetComicInfo(filePath);
        }

        return null;
    }

    /// <summary>
    /// Processes files found during a library scan.
    /// </summary>
    /// <param name="path">Path of a file</param>
    /// <param name="rootPath"></param>
    /// <param name="type">Library type to determine parsing to perform</param>
    public ParserInfo? ParseFile(string path, string rootPath, LibraryType type)
    {
        var info = Parse(path, rootPath, type);
        if (info == null)
        {
            return null;
        }


        // This catches when original library type is Manga/Comic and when parsing with non
        if (Tasks.Scanner.Parser.Parser.IsEpub(path) && Tasks.Scanner.Parser.Parser.ParseVolume(info.Series) != Tasks.Scanner.Parser.Parser.DefaultVolume) // Shouldn't this be info.Volume != DefaultVolume?
        {
            var hasVolumeInTitle = !Tasks.Scanner.Parser.Parser.ParseVolume(info.Title)
                    .Equals(Tasks.Scanner.Parser.Parser.DefaultVolume);
            var hasVolumeInSeries = !Tasks.Scanner.Parser.Parser.ParseVolume(info.Series)
                .Equals(Tasks.Scanner.Parser.Parser.DefaultVolume);

            if (string.IsNullOrEmpty(info.ComicInfo?.Volume) && hasVolumeInTitle && (hasVolumeInSeries || string.IsNullOrEmpty(info.Series)))
            {
                // This is likely a light novel for which we can set series from parsed title
                info.Series = Tasks.Scanner.Parser.Parser.ParseSeries(info.Title);
                info.Volumes = Tasks.Scanner.Parser.Parser.ParseVolume(info.Title);
            }
            else
            {
                var info2 = _defaultParser.Parse(path, rootPath, LibraryType.Book);
                info.Merge(info2);
            }

        }

        info.ComicInfo = GetComicInfo(path);
        if (info.ComicInfo == null) return info;

        if (!string.IsNullOrEmpty(info.ComicInfo.Volume))
        {
            info.Volumes = info.ComicInfo.Volume;
        }
        if (!string.IsNullOrEmpty(info.ComicInfo.Series))
        {
            info.Series = info.ComicInfo.Series.Trim();
        }
        if (!string.IsNullOrEmpty(info.ComicInfo.Number))
        {
            info.Chapters = info.ComicInfo.Number;
        }

        // Patch is SeriesSort from ComicInfo
        if (!string.IsNullOrEmpty(info.ComicInfo.TitleSort))
        {
            info.SeriesSort = info.ComicInfo.TitleSort.Trim();
        }

        if (!string.IsNullOrEmpty(info.ComicInfo.Format) && Tasks.Scanner.Parser.Parser.HasComicInfoSpecial(info.ComicInfo.Format))
        {
            info.IsSpecial = true;
            info.Chapters = Tasks.Scanner.Parser.Parser.DefaultChapter;
            info.Volumes = Tasks.Scanner.Parser.Parser.DefaultVolume;
        }

        if (!string.IsNullOrEmpty(info.ComicInfo.SeriesSort))
        {
            info.SeriesSort = info.ComicInfo.SeriesSort.Trim();
        }

        if (!string.IsNullOrEmpty(info.ComicInfo.LocalizedSeries))
        {
            info.LocalizedSeries = info.ComicInfo.LocalizedSeries.Trim();
        }

        return info;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    public int GetNumberOfPages(string filePath, MangaFormat format)
    {
        switch (format)
        {
            case MangaFormat.Archive:
            {
                return _archiveService.GetNumberOfPagesFromArchive(filePath);
            }
            case MangaFormat.Pdf:
            case MangaFormat.Epub:
            {
                return _bookService.GetNumberOfPages(filePath);
            }
            case MangaFormat.Image:
            {
                return 1;
            }
            case MangaFormat.Unknown:
            default:
                return 0;
        }
    }

    public string GetCoverImage(string filePath, string fileName, MangaFormat format, bool saveAsWebP)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }


        return format switch
        {
            MangaFormat.Epub => _bookService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, saveAsWebP),
            MangaFormat.Archive => _archiveService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, saveAsWebP),
            MangaFormat.Image => _imageService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, saveAsWebP),
            MangaFormat.Pdf => _bookService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, saveAsWebP),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Extracts the reading item to the target directory using the appropriate method
    /// </summary>
    /// <param name="fileFilePath">File to extract</param>
    /// <param name="targetDirectory">Where to extract to. Will be created if does not exist</param>
    /// <param name="format">Format of the File</param>
    /// <param name="imageCount">If the file is of type image, pass number of files needed. If > 0, will copy the whole directory.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1)
    {
        switch (format)
        {
            case MangaFormat.Archive:
                _archiveService.ExtractArchive(fileFilePath, targetDirectory);
                break;
            case MangaFormat.Image:
                _imageService.ExtractImages(fileFilePath, targetDirectory, imageCount);
                break;
            case MangaFormat.Pdf:
                _bookService.ExtractPdfImages(fileFilePath, targetDirectory);
                break;
            case MangaFormat.Unknown:
            case MangaFormat.Epub:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    /// <summary>
    /// Parses information out of a file. If file is a book (epub), it will use book metadata regardless of LibraryType
    /// </summary>
    /// <param name="path"></param>
    /// <param name="rootPath"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    private ParserInfo? Parse(string path, string rootPath, LibraryType type)
    {
        return Tasks.Scanner.Parser.Parser.IsEpub(path) ? _bookService.ParseInfo(path) : _defaultParser.Parse(path, rootPath, type);
    }
}
