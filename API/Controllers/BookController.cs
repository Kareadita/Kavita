using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Reader;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VersOne.Epub;

namespace API.Controllers;

#nullable enable

public class BookController : BaseApiController
{
    private readonly IBookService _bookService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILocalizationService _localizationService;

    public BookController(IBookService bookService,
        IUnitOfWork unitOfWork, ICacheService cacheService,
        ILocalizationService localizationService)
    {
        _bookService = bookService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Retrieves information for the PDF and Epub reader
    /// </summary>
    /// <remarks>This only applies to Epub or PDF files</remarks>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("{chapterId}/book-info")]
    public async Task<ActionResult<BookInfoDto>> GetBookInfo(int chapterId)
    {
        var dto = await _unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(chapterId);
        if (dto == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));
        var bookTitle = string.Empty;
        switch (dto.SeriesFormat)
        {
            case MangaFormat.Epub:
            {
                var mangaFile = (await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId))[0];
                using var book = await EpubReader.OpenBookAsync(mangaFile.FilePath, BookService.BookReaderOptions);
                bookTitle = book.Title;
                break;
            }
            case MangaFormat.Pdf:
            {
                var mangaFile = (await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId))[0];
                if (string.IsNullOrEmpty(bookTitle))
                {
                    // Override with filename
                    bookTitle = Path.GetFileNameWithoutExtension(mangaFile.FilePath);
                }

                break;
            }
            case MangaFormat.Image:
            case MangaFormat.Archive:
            case MangaFormat.Unknown:
            default:
                break;
        }

        return Ok(new BookInfoDto()
        {
            ChapterNumber =  dto.ChapterNumber,
            VolumeNumber = dto.VolumeNumber,
            VolumeId = dto.VolumeId,
            BookTitle = bookTitle,
            SeriesName = dto.SeriesName,
            SeriesFormat = dto.SeriesFormat,
            SeriesId = dto.SeriesId,
            LibraryId = dto.LibraryId,
            IsSpecial = dto.IsSpecial,
            Pages = dto.Pages,
        });
    }

    /// <summary>
    /// This is an entry point to fetch resources from within an epub chapter/book.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="file"></param>
    /// <returns></returns>
    [HttpGet("{chapterId}/book-resources")]
    [ResponseCache(Duration = 60 * 1, Location = ResponseCacheLocation.Client, NoStore = false)]
    [AllowAnonymous]
    public async Task<ActionResult> GetBookPageResources(int chapterId, [FromQuery] string file)
    {
        if (chapterId <= 0) return BadRequest(await _localizationService.Get("en", "chapter-doesnt-exist"));
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null) return BadRequest(await _localizationService.Get("en", "chapter-doesnt-exist"));

        using var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath, BookService.BookReaderOptions);
        var key = BookService.CoalesceKeyForAnyFile(book, file);

        if (!book.Content.AllFiles.ContainsLocalFileRefWithKey(key)) return BadRequest(await _localizationService.Get("en", "file-missing"));

        var bookFile = book.Content.AllFiles.GetLocalFileRefByKey(key);
        var content = await bookFile.ReadContentAsBytesAsync();

        var contentType = BookService.GetContentType(bookFile.ContentType);
        return File(content, contentType, $"{chapterId}-{file}");
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
        if (chapterId <= 0) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));

        try
        {
            return Ok(await _bookService.GenerateTableOfContents(chapter));
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
        var chapter = await _cacheService.Ensure(chapterId);
        if (chapter == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));
        var path = _cacheService.GetCachedFile(chapter);

        var baseUrl = "//" + Request.Host + Request.PathBase + "/api/";

        try
        {
            return Ok(await _bookService.GetBookPage(page, chapterId, path, baseUrl));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }
    }
}
