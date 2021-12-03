﻿using System;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Parser;

namespace API.Services;

public interface IReadingItemService
{
    ComicInfo GetComicInfo(string filePath, MangaFormat format);
    int GetNumberOfPages(string filePath, MangaFormat format);
    string GetCoverImage(string fileFilePath, string fileName, MangaFormat format);
    void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1);
    ParserInfo Parse(string path, string rootPath, LibraryType type);
}

public class ReadingItemService : IReadingItemService
{
    private readonly IArchiveService _archiveService;
    private readonly IBookService _bookService;
    private readonly IImageService _imageService;
    private readonly DefaultParser _defaultParser;

    public ReadingItemService(IArchiveService archiveService, IBookService bookService, IImageService imageService, IDirectoryService directoryService)
    {
        _archiveService = archiveService;
        _bookService = bookService;
        _imageService = imageService;

        _defaultParser = new DefaultParser(directoryService);
    }

    /// <summary>
    /// Gets the ComicInfo for the file if it exists. Null otherewise.
    /// </summary>
    /// <param name="filePath">Fully qualified path of file</param>
    /// <param name="format">Format of the file determines how we open it (epub vs comicinfo.xml)</param>
    /// <returns></returns>
    public ComicInfo? GetComicInfo(string filePath, MangaFormat format)
    {
        if (format is MangaFormat.Archive or MangaFormat.Epub)
        {
            return Parser.Parser.IsEpub(filePath) ? _bookService.GetComicInfo(filePath) : _archiveService.GetComicInfo(filePath);
        }

        return null;
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

    public string GetCoverImage(string filePath, string fileName, MangaFormat format)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }
        return format switch
        {
            MangaFormat.Epub => _bookService.GetCoverImage(filePath, fileName),
            MangaFormat.Archive => _archiveService.GetCoverImage(filePath, fileName),
            MangaFormat.Image => _imageService.GetCoverImage(filePath, fileName),
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
            case MangaFormat.Pdf:
                _bookService.ExtractPdfImages(fileFilePath, targetDirectory);
                break;
            case MangaFormat.Archive:
                _archiveService.ExtractArchive(fileFilePath, targetDirectory);
                break;
            case MangaFormat.Image:
                _imageService.ExtractImages(fileFilePath, targetDirectory, imageCount);
                break;
            case MangaFormat.Unknown:
            case MangaFormat.Epub:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    public ParserInfo Parse(string path, string rootPath, LibraryType type)
    {
        return Parser.Parser.IsEpub(path) ? _bookService.ParseInfo(path) : _defaultParser.Parse(path, rootPath, type);
    }
}
