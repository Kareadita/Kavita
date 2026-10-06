using System;
using System.IO;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Reading;

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
    /// <param name="enableMetadata">If false, returns null</param>
    /// <param name="issue">Set when the metadata could not be read, or an epub only opened leniently. The file can still be imported</param>
    private ComicInfo? ReadComicInfo(string filePath, bool enableMetadata, out ParseIssue? issue)
    {
        issue = null;
        if (!enableMetadata) return null;

        try
        {
            if (Parser.IsEpub(filePath) || Parser.IsPdf(filePath))
            {
                var comicInfo = _bookService.GetComicInfo(filePath, out var strictOpenError);
                if (strictOpenError != null)
                {
                    issue = new ParseIssue(MediaErrorReason.EpubNotStrict, "The epub only opened with lenient parsing", strictOpenError);
                }

                return comicInfo;
            }

            if (Parser.IsComicInfoExtension(filePath))
            {
                return _archiveService.GetComicInfo(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "There was an exception reading the metadata of {FilePath}, parsing without it", filePath);
            issue = new ParseIssue(MediaErrorReason.MetadataUnreadable, "There was an exception reading the metadata of the file",
                ParseIssues.Describe(ex));
        }

        return null;
    }

    /// <summary>
    /// Processes files found during a library scan. A failure is returned for the scanner to record, not reported here
    /// </summary>
    /// <param name="path">Path of a file</param>
    /// <param name="rootPath"></param>
    /// <param name="libraryRoot"></param>
    /// <param name="type">Library type to determine parsing to perform</param>
    /// <param name="enableMetadata">Enable Metadata parsing overriding filename parsing</param>
    public ParseFileResult ParseFile(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata)
    {
        var parser = ParserFor(path, type);
        if (parser == null)
        {
            _logger.LogError("No parser can read file {FilePath}", path);
            return ParseFileResult.Failed(ParseIssues.FromFailedParse(null));
        }

        var comicInfo = ReadComicInfo(path, enableMetadata, out var metadataIssue);
        try
        {
            var parseResult = parser.Parse(path, rootPath, libraryRoot, type, enableMetadata, comicInfo);
            if (!parseResult.Success)
            {
                _logger.LogError("Unable to parse any meaningful information out of file {FilePath}. Found {@ParserInfo}",
                    path, parseResult.Info);

                return ParseFileResult.Failed(ParseIssues.FromFailedParse(parseResult.Info));
            }

            return parseResult.Info == null ? new ParseFileResult(null) : new ParseFileResult(parseResult.Info, metadataIssue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when parsing file {FilePath}", path);
            return ParseFileResult.Failed(ParseIssues.FromException(path, "There was an exception when parsing file", ex));
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
    /// The parser for a file. A book (epub) uses book metadata regardless of LibraryType
    /// </summary>
    private DefaultParser? ParserFor(string path, LibraryType type)
    {
        if (_comicVineParser.IsApplicable(path, type)) return _comicVineParser;
        if (_imageParser.IsApplicable(path, type)) return _imageParser;
        if (_bookParser.IsApplicable(path, type)) return _bookParser;
        if (_pdfParser.IsApplicable(path, type)) return _pdfParser;
        if (_basicParser.IsApplicable(path, type)) return _basicParser;

        return null;
    }
}
