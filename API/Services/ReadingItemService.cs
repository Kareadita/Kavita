using System;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;

namespace API.Services;
#nullable enable

public interface IReadingItemService
{
    int GetNumberOfPages(string filePath, MangaFormat format);
    string GetCoverImage(string filePath, string fileName, MangaFormat format, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
    void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1);
    ParserInfo? ParseFile(string path, string rootPath, string libraryRoot, LibraryType type);
}

public class ReadingItemService : IReadingItemService
{
    private readonly IArchiveService _archiveService;
    private readonly IBookService _bookService;
    private readonly IImageService _imageService;
    private readonly IDirectoryService _directoryService;
    private readonly ILogger<ReadingItemService> _logger;
    private readonly BasicParser _basicParser;
    private readonly ComicVineParser _comicVineParser;
    private readonly ImageParser _imageParser;
    private readonly BookParser _bookParser;
    private readonly PdfParser _pdfParser;

    public ReadingItemService(IArchiveService archiveService, IBookService bookService, IImageService imageService,
        IDirectoryService directoryService, ILogger<ReadingItemService> logger)
    {
        _archiveService = archiveService;
        _bookService = bookService;
        _imageService = imageService;
        _directoryService = directoryService;
        _logger = logger;

        _imageParser = new ImageParser(directoryService);
        _basicParser = new BasicParser(directoryService, _imageParser);
        _bookParser = new BookParser(directoryService, bookService, _basicParser);
        _comicVineParser = new ComicVineParser(directoryService);
        _pdfParser = new PdfParser(directoryService);

    }

    /// <summary>
    /// Gets the ComicInfo for the file if it exists. Null otherwise.
    /// </summary>
    /// <param name="filePath">Fully qualified path of file</param>
    /// <returns></returns>
    private ComicInfo? GetComicInfo(string filePath)
    {
        if (Parser.IsEpub(filePath) || Parser.IsPdf(filePath))
        {
            return _bookService.GetComicInfo(filePath);
        }

        if (Parser.IsComicInfoExtension(filePath))
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
    public ParserInfo? ParseFile(string path, string rootPath, string libraryRoot, LibraryType type)
    {
        try
        {
            var info = Parse(path, rootPath, libraryRoot, type);
            if (info == null)
            {
                _logger.LogError("Unable to parse any meaningful information out of file {FilePath}", path);
                return null;
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when parsing file {FilePath}", path);
            return null;
        }
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

    public string GetCoverImage(string filePath, string fileName, MangaFormat format, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }


        return format switch
        {
            MangaFormat.Epub => _bookService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, encodeFormat, size),
            MangaFormat.Archive => _archiveService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, encodeFormat, size),
            MangaFormat.Image => _imageService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, encodeFormat, size),
            MangaFormat.Pdf => _bookService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, encodeFormat, size),
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
    private ParserInfo? Parse(string path, string rootPath, string libraryRoot, LibraryType type)
    {
        if (_comicVineParser.IsApplicable(path, type))
        {
            return _comicVineParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }
        if (_imageParser.IsApplicable(path, type))
        {
            return _imageParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }
        if (_bookParser.IsApplicable(path, type))
        {
            return _bookParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }
        if (_pdfParser.IsApplicable(path, type))
        {
            return _pdfParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }
        if (_basicParser.IsApplicable(path, type))
        {
            return _basicParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }

        return null;
    }
}
