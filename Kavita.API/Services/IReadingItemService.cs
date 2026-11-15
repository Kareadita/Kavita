using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Models.Parser;

namespace Kavita.API.Services;

public interface IReadingItemService
{
    int GetNumberOfPages(string filePath, MangaFormat format);
    /// <summary>
    /// Opens the file's cover image for the caller to write, then dispose. Null when there is none to read
    /// </summary>
    OpenedCover? OpenCover(string filePath, MangaFormat format);
    void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1);
    ParseFileResult ParseFile(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata);
}
