using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services;

public interface IReadingItemService
{
    ComicInfo GetComicInfo(string filePath, MangaFormat format);
    int GetNumberOfPages(string filePath);
    string GetCoverImage(string fileFilePath, string fileName);
    void Extract(string fileFilePath, string targetDirectory);
}

public class ReadingItemService : IReadingItemService
{
    private readonly IArchiveService _archiveService;
    private readonly IBookService _bookService;

    public ReadingItemService(IArchiveService archiveService, IBookService bookService)
    {
        _archiveService = archiveService;
        _bookService = bookService;
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

    public string GetCoverImage(string fileFilePath, string fileName)
    {
        throw new System.NotImplementedException();
    }

    public void Extract(string fileFilePath, string targetDirectory)
    {
        throw new System.NotImplementedException();
    }
}
