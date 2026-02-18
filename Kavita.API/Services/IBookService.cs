using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using VersOne.Epub;

namespace Kavita.API.Services;

public interface IBookService
{
    int GetNumberOfPages(string filePath);
    string GetCoverImage(string fileFilePath, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
    ComicInfo? GetComicInfo(string filePath);
    ParserInfo? ParseInfo(string filePath);

    /// <summary>
    /// Scopes styles to .reading-section and replaces img src to the passed apiBase
    /// </summary>
    /// <param name="stylesheetHtml"></param>
    /// <param name="apiBase"></param>
    /// <param name="filename">If the stylesheetHtml contains Import statements, when scoping the filename, scope needs to be wrt filepath.</param>
    /// <param name="book">Book Reference, needed for if you expect Import statements</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<string> ScopeStyles(string stylesheetHtml, string apiBase, string filename, EpubBookRef book, CancellationToken ct = default);
    /// <summary>
    /// Extracts a PDF file's pages as images to a target directory
    /// </summary>
    /// <remarks>This method relies on Docnet which has explicit patches from Kavita for ARM support. This should only be used with Tachiyomi</remarks>
    /// <param name="fileFilePath"></param>
    /// <param name="targetDirectory">Where the files will be extracted to. If doesn't exist, will be created.</param>
    void ExtractPdfImages(string fileFilePath, string targetDirectory);
    Task<ICollection<BookChapterItem>> GenerateTableOfContents(Chapter chapter, CancellationToken ct = default);
    Task<string> GetBookPage(int page, int chapterId, string cachedEpubPath, string baseUrl, List<PersonalToCDto> ptocBookmarks, List<AnnotationDto> annotations, CancellationToken ct = default);
    Task<Dictionary<string, int>> CreateKeyToPageMappingAsync(EpubBookRef book, CancellationToken ct = default);
    Task<IDictionary<int, int>?> GetWordCountsPerPage(string bookFilePath, CancellationToken ct = default);
    Task<int> GetWordCountBetweenXPaths(string bookFilePath, string startXpath, int startPage, string endXpath, int endPage, CancellationToken ct = default);
    Task<string> CopyImageToTempFromBook(int chapterId, BookmarkDto bookmarkDto, string cachedBookPath, CancellationToken ct = default);
    Task<BookResourceResultDto> GetResourceAsync(string bookFilePath, string requestedKey, CancellationToken ct = default);
}
