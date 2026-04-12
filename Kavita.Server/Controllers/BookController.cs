using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Kavita.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VersOne.Epub;

namespace Kavita.Server.Controllers;

public class BookController(
    IBookService bookService,
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    ILocalizationService localizationService)
    : BaseApiController
{
    /// <summary>
    /// Retrieves information for the PDF and Epub reader. This will cache the file.
    /// </summary>
    /// <remarks>This only applies to Epub or PDF files</remarks>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("{chapterId}/book-info")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour, VaryByQueryKeys = ["chapterId"])]
    public async Task<ActionResult<BookInfoDto>> GetBookInfo(int chapterId)
    {
        var dto = await unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(chapterId);
        if (dto == null) return BadRequest(await localizationService.TranslateAsync(UserId, "chapter-doesnt-exist"));
        var bookTitle = string.Empty;


        switch (dto.SeriesFormat)
        {
            case MangaFormat.Epub:
            {
                var mangaFile = (await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId))[0];
                await cacheService.Ensure(chapterId);

                var file = cacheService.GetCachedFile(chapterId, mangaFile.FilePath);
                using var book = await EpubReader.OpenBookAsync(file, BookService.LenientBookReaderOptions);
                if (book == null) return NotFound();

                bookTitle = book.Title;

                break;
            }
            case MangaFormat.Pdf:
            {
                var mangaFile = (await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId))[0];
                await cacheService.Ensure(chapterId);

                var file = cacheService.GetCachedFile(chapterId, mangaFile.FilePath);
                if (string.IsNullOrEmpty(bookTitle))
                {
                    // Override with filename
                    bookTitle = Path.GetFileNameWithoutExtension(file);
                }

                break;
            }
            case MangaFormat.Image:
            case MangaFormat.Archive:
            case MangaFormat.Unknown:
            default:
                break;
        }

        var info = new BookInfoDto()
        {
            ChapterNumber = dto.ChapterNumber,
            VolumeNumber = dto.VolumeNumber,
            VolumeId = dto.VolumeId,
            BookTitle = bookTitle,
            SeriesName = dto.SeriesName,
            SeriesFormat = dto.SeriesFormat,
            SeriesId = dto.SeriesId,
            LibraryId = dto.LibraryId,
            IsSpecial = dto.IsSpecial,
            Pages = dto.Pages,
        };


        return Ok(info);
    }

    /// <summary>
    /// This is an entry point to fetch resources from within an epub chapter/book.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="file"></param>
    /// <returns></returns>
    [ChapterAccess]
    [SkipDeviceTracking]
    [HttpGet("{chapterId}/book-resources")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = ["chapterId", "file"])]
    public async Task<ActionResult> GetBookPageResources(int chapterId, [FromQuery] string file)
    {
        if (chapterId <= 0) return BadRequest(await localizationService.GetAsync("en", "chapter-doesnt-exist"));

        var chapter = await cacheService.Ensure(chapterId);
        if (chapter == null) return BadRequest(await localizationService.GetAsync("en", "chapter-doesnt-exist"));

        var cachedFilePath = Path.Join(cacheService.GetCachePath(chapterId), Path.GetFileName(chapter.Files.ElementAt(0).FilePath));
        var result = await bookService.GetResourceAsync(cachedFilePath, file);

        if (!result.IsSuccess) return BadRequest(await localizationService.GetAsync("en", result.ErrorMessage));

        return File(result.Content, result.ContentType, $"{chapterId}-{file}");
    }

    /// <summary>
    /// This will return a list of mappings from ID -> page num. ID will be the xhtml key and page num will be the reading order
    /// this is used to rewrite anchors in the book text so that we always load properly in our reader.
    /// </summary>
    /// <remarks>This is essentially building the table of contents</remarks>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("{chapterId}/chapters")]
    public async Task<ActionResult<ICollection<BookChapterItem>>> GetBookChapters(int chapterId)
    {
        if (chapterId <= 0) return BadRequest(await localizationService.TranslateAsync(UserId, "chapter-doesnt-exist"));

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null) return BadRequest(await localizationService.TranslateAsync(UserId, "chapter-doesnt-exist"));

        try
        {
            return Ok(await bookService.GenerateTableOfContents(chapter));
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }


    /// <summary>
    /// This returns a single page within the epub book. All html will be rewritten to be scoped within our reader,
    /// all css is scoped, etc.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("{chapterId}/book-page")]
    public async Task<ActionResult<string>> GetBookPage(int chapterId, [FromQuery] int page)
    {
        var chapter = await cacheService.Ensure(chapterId);
        if (chapter == null) return BadRequest(await localizationService.TranslateAsync(UserId, "chapter-doesnt-exist"));
        var path = cacheService.GetCachedFile(chapter);

        var baseUrl = "//" + Request.Host + Request.PathBase + "/api/";

        try
        {
            var ptocBookmarks =
                await unitOfWork.UserTableOfContentRepository.GetPersonalToCForPage(UserId, chapterId, page);
            var annotations = await unitOfWork.UserRepository.GetAnnotationsByPage(UserId, chapter.Id, page);

            return Ok(await bookService.GetBookPage(UserId, page, chapterId, path, baseUrl, ptocBookmarks, annotations));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
        }
    }
}
