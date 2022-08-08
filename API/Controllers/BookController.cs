using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Reader;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VersOne.Epub;

namespace API.Controllers
{
    public class BookController : BaseApiController
    {
        private readonly ILogger<BookController> _logger;
        private readonly IBookService _bookService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;
        private const string BookApiUrl = "book-resources?file=";


        public BookController(ILogger<BookController> logger, IBookService bookService,
            IUnitOfWork unitOfWork, ICacheService cacheService)
        {
            _logger = logger;
            _bookService = bookService;
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
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
            var bookTitle = string.Empty;
            switch (dto.SeriesFormat)
            {
                case MangaFormat.Epub:
                {
                    var mangaFile = (await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId)).First();
                    using var book = await EpubReader.OpenBookAsync(mangaFile.FilePath, BookService.BookReaderOptions);
                    bookTitle = book.Title;
                    break;
                }
                case MangaFormat.Pdf:
                {
                    var mangaFile = (await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId)).First();
                    if (string.IsNullOrEmpty(bookTitle))
                    {
                        // Override with filename
                        bookTitle = Path.GetFileNameWithoutExtension(mangaFile.FilePath);
                    }

                    break;
                }
                case MangaFormat.Image:
                    break;
                case MangaFormat.Archive:
                    break;
                case MangaFormat.Unknown:
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
        [AllowAnonymous]
        public async Task<ActionResult> GetBookPageResources(int chapterId, [FromQuery] string file)
        {
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
            using var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath, BookService.BookReaderOptions);

            var key = BookService.CleanContentKeys(file);
            if (!book.Content.AllFiles.ContainsKey(key)) return BadRequest("File was not found in book");

            var bookFile = book.Content.AllFiles[key];
            var content = await bookFile.ReadContentAsBytesAsync();

            Response.AddCacheHeader(content);
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
            var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
            using var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath, BookService.BookReaderOptions);
            var mappings = await _bookService.CreateKeyToPageMappingAsync(book);

            var navItems = await book.GetNavigationAsync();
            var chaptersList = new List<BookChapterItem>();

            foreach (var navigationItem in navItems)
            {
                if (navigationItem.NestedItems.Count > 0)
                {
                   var nestedChapters = new List<BookChapterItem>();

                    foreach (var nestedChapter in navigationItem.NestedItems)
                    {
                        if (nestedChapter.Link == null) continue;
                        var key = BookService.CleanContentKeys(nestedChapter.Link.ContentFileName);
                        if (mappings.ContainsKey(key))
                        {
                            nestedChapters.Add(new BookChapterItem()
                            {
                                Title = nestedChapter.Title,
                                Page = mappings[key],
                                Part = nestedChapter.Link.Anchor ?? string.Empty,
                                Children = new List<BookChapterItem>()
                            });
                        }
                    }

                    CreateToCChapter(navigationItem, nestedChapters, chaptersList, mappings);
                }

                if (navigationItem.NestedItems.Count == 0)
                {
                    CreateToCChapter(navigationItem, Array.Empty<BookChapterItem>(), chaptersList, mappings);
                }
            }

            if (chaptersList.Count == 0)
            {
                // Generate from TOC
                var tocPage = book.Content.Html.Keys.FirstOrDefault(k => k.ToUpper().Contains("TOC"));
                if (tocPage == null) return Ok(chaptersList);

                // Find all anchor tags, for each anchor we get inner text, to lower then title case on UI. Get href and generate page content
                var doc = new HtmlDocument();
                var content = await book.Content.Html[tocPage].ReadContentAsync();
                doc.LoadHtml(content);
                var anchors = doc.DocumentNode.SelectNodes("//a");
                if (anchors == null) return Ok(chaptersList);

                foreach (var anchor in anchors)
                {
                    if (anchor.Attributes.Contains("href"))
                    {
                        var key = BookService.CleanContentKeys(anchor.Attributes["href"].Value).Split("#")[0];
                        if (!mappings.ContainsKey(key))
                        {
                            // Fallback to searching for key (bad epub metadata)
                            var correctedKey = book.Content.Html.Keys.SingleOrDefault(s => s.EndsWith(key));
                            if (!string.IsNullOrEmpty(correctedKey))
                            {
                                key = correctedKey;
                            }
                        }
                        if (!string.IsNullOrEmpty(key) && mappings.ContainsKey(key))
                        {
                            var part = string.Empty;
                            if (anchor.Attributes["href"].Value.Contains('#'))
                            {
                                part = anchor.Attributes["href"].Value.Split("#")[1];
                            }
                            chaptersList.Add(new BookChapterItem()
                            {
                                Title = anchor.InnerText,
                                Page = mappings[key],
                                Part = part,
                                Children = new List<BookChapterItem>()
                            });
                        }
                    }
                }

            }
            return Ok(chaptersList);
        }

        private static void CreateToCChapter(EpubNavigationItemRef navigationItem, IList<BookChapterItem> nestedChapters, IList<BookChapterItem> chaptersList,
            IReadOnlyDictionary<string, int> mappings)
        {
            if (navigationItem.Link == null)
            {
                var item = new BookChapterItem()
                {
                    Title = navigationItem.Title,
                    Children = nestedChapters
                };
                if (nestedChapters.Count > 0)
                {
                    item.Page = nestedChapters[0].Page;
                }

                chaptersList.Add(item);
            }
            else
            {
                var groupKey = BookService.CleanContentKeys(navigationItem.Link.ContentFileName);
                if (mappings.ContainsKey(groupKey))
                {
                    chaptersList.Add(new BookChapterItem()
                    {
                        Title = navigationItem.Title,
                        Page = mappings[groupKey],
                        Children = nestedChapters
                    });
                }
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
            var path = _cacheService.GetCachedFile(chapter);

            using var book = await EpubReader.OpenBookAsync(path, BookService.BookReaderOptions);
            var mappings = await _bookService.CreateKeyToPageMappingAsync(book);

            var counter = 0;
            var doc = new HtmlDocument {OptionFixNestedTags = true};

            var baseUrl = "//" + Request.Host + Request.PathBase + "/api/";
            var apiBase = baseUrl + "book/" + chapterId + "/" + BookApiUrl;
            var bookPages = await book.GetReadingOrderAsync();
            foreach (var contentFileRef in bookPages)
            {
                if (page != counter)
                {
                    counter++;
                    continue;
                }

                var content = await contentFileRef.ReadContentAsync();
                if (contentFileRef.ContentType != EpubContentType.XHTML_1_1) return Ok(content);

                // In more cases than not, due to this being XML not HTML, we need to escape the script tags.
                content = BookService.EscapeTags(content);

                doc.LoadHtml(content);
                var body = doc.DocumentNode.SelectSingleNode("//body");

                if (body == null)
                {
                    if (doc.ParseErrors.Any())
                    {
                        LogBookErrors(book, contentFileRef, doc);
                        return BadRequest("The file is malformed! Cannot read.");
                    }
                    _logger.LogError("{FilePath} has no body tag! Generating one for support. Book may be skewed", book.FilePath);
                    doc.DocumentNode.SelectSingleNode("/html").AppendChild(HtmlNode.CreateNode("<body></body>"));
                    body = doc.DocumentNode.SelectSingleNode("/html/body");
                }

                return Ok(await _bookService.ScopePage(doc, book, apiBase, body, mappings, page));
            }

            return BadRequest("Could not find the appropriate html for that page");
        }

        private void LogBookErrors(EpubBookRef book, EpubContentFileRef contentFileRef, HtmlDocument doc)
        {
            _logger.LogError("{FilePath} has an invalid html file (Page {PageName})", book.FilePath, contentFileRef.FileName);
            foreach (var error in doc.ParseErrors)
            {
                _logger.LogError("Line {LineNumber}, Reason: {Reason}", error.Line, error.Reason);
            }
        }
    }
}
