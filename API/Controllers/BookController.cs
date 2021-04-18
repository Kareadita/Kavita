using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using API.DTOs;
using API.Entities.Interfaces;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
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
        public readonly static string BookApiUrl = "book-resources?file=";
        
        

        public BookController(ILogger<BookController> logger, IBookService bookService, IUnitOfWork unitOfWork, ICacheService cacheService)
        {
            _logger = logger;
            _bookService = bookService;
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        [HttpGet("{chapterId}/book-resources")]
        public async Task<ActionResult> GetBookPageResources(int chapterId, [FromQuery] string file)
        {
            var chapter = await _cacheService.Ensure(chapterId);
            var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath);

            var key = BookService.CleanContentKeys(file);
            if (!book.Content.AllFiles.ContainsKey(key)) return BadRequest("File was not found in book");
            
            var bookFile = book.Content.AllFiles[key];
            var content = await bookFile.ReadContentAsBytesAsync();
            Response.AddCacheHeader(content);
            var contentType = string.Empty;
            switch (bookFile.ContentType)
            {
                case EpubContentType.IMAGE_GIF:
                    contentType = "image/gif";
                    break;
                case EpubContentType.IMAGE_PNG:
                    contentType = "image/png";
                    break;
                case EpubContentType.IMAGE_JPEG:
                    contentType = "image/jpeg";
                    break;
                case EpubContentType.FONT_OPENTYPE:
                    contentType = "font/otf";
                    break;
                case EpubContentType.FONT_TRUETYPE:
                    contentType = "font/ttf";
                    break;
                case EpubContentType.IMAGE_SVG:
                    contentType = "image/svg+xml";
                    break;
                default:
                    contentType = "application/octet-stream";
                    break;
                
            }
            return File(content, contentType, $"{chapterId}-{file}");
        }

        [HttpGet("{chapterId}/chapters")]
        public async Task<ActionResult<ICollection<BookChapterItem>>> GetBookChapters(int chapterId)
        {
            // This will return a list of mappings from ID -> pagenum. ID will be the xhtml key and pagenum will be the reading order
            // this is used to rewrite anchors in the book text so that we always load properly in FE
            var chapter = await _cacheService.Ensure(chapterId);
            var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath);
            var mappings = await _bookService.CreateKeyToPageMappingAsync(book);
            
            var navItems = await book.GetNavigationAsync();
            var chaptersList = new List<BookChapterItem>();
            
            foreach (var navigationItem in navItems)
            {
                if (navigationItem.NestedItems.Count > 0)
                {
                    _logger.LogDebug("Header: {Header}", navigationItem.Title);
                    var nestedChapters = new List<BookChapterItem>();
                    
                    foreach (var nestedChapter in navigationItem.NestedItems)
                    {
                        if (nestedChapter.Link == null) continue;
                        var key = BookService.CleanContentKeys(nestedChapter.Link.ContentFileName);
                        if (mappings.ContainsKey(key))
                        {
                            var partTokens = nestedChapter.Link.ContentFileName.Split('#');

                            nestedChapters.Add(new BookChapterItem()
                            {
                                Title = nestedChapter.Title,
                                Page = mappings[key],
                                Part = partTokens.Length > 1 ? partTokens[1] : string.Empty,
                                Children = new List<BookChapterItem>()
                            });
                        }
                    }

                    if (navigationItem.Link == null)
                    {
                        chaptersList.Add(new BookChapterItem()
                        {
                            Title = navigationItem.Title,
                            Children = nestedChapters
                        });
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
            }
            return Ok(chaptersList);
        }
        
        [HttpGet("{chapterId}/book-page")]
        public async Task<ActionResult<string>> GetBookPage(int chapterId, [FromQuery] int page, [FromQuery] string baseUrl)
        {
            _logger.LogDebug("Book endpoint hit");
            var chapter = await _cacheService.Ensure(chapterId);

            var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath);
            var mappings = await _bookService.CreateKeyToPageMappingAsync(book);

            var counter = 0;
            var doc = new HtmlDocument();
            
            var apiBase = baseUrl + "book/" + chapterId + "/" + BookApiUrl;
            var bookPages = await book.GetReadingOrderAsync();
            foreach (var contentFileRef in bookPages)
            {
                if (page == counter)
                {
                    var content = await contentFileRef.ReadContentAsync();
                    if (contentFileRef.ContentType != EpubContentType.XHTML_1_1) return Ok(content);
                    
                    doc.LoadHtml(content);
                    var body = doc.DocumentNode.SelectSingleNode("/html/body");
                    
                    var inlineStyles = doc.DocumentNode.SelectNodes("//style");
                    if (inlineStyles != null)
                    {
                        foreach (var inlineStyle in inlineStyles)
                        {
                            var styleContent =
                                Parser.Parser.FontSrcUrlRegex.Replace(inlineStyle.InnerHtml, "$1" + apiBase + "$2" + "$3");
                            body.PrependChild(HtmlNode.CreateNode($"<style>{styleContent}</style>"));
                        }
                    }
                    
                    var styleNodes = doc.DocumentNode.SelectNodes("/html/head/link");
                    if (styleNodes != null)
                    {
                        foreach (var styleLinks in styleNodes)
                        {
                            var key = BookService.CleanContentKeys(styleLinks.Attributes["href"].Value);
                            var styleContent = BookService.RemoveWhiteSpaceFromStylesheets(await book.Content.Css[key].ReadContentAsync());
                            styleContent =
                                Parser.Parser.FontSrcUrlRegex.Replace(styleContent, "$1" + apiBase + "$2" + "$3");
                            body.PrependChild(HtmlNode.CreateNode($"<style>{styleContent}</style>"));
                        }
                    }

                    var anchors = doc.DocumentNode.SelectNodes("//a");
                    if (anchors != null)
                    {
                        foreach (var anchor in anchors)
                        {
                            if (anchor.Name != "a") continue;
                            
                            var mappingKey = BookService.CleanContentKeys(anchor.GetAttributeValue("href", string.Empty)).Split("#")[0];
                            if (!mappings.ContainsKey(mappingKey))
                            {
                                if (HasClickableHrefPart(anchor))
                                {
                                    // TODO: If there are anchors like href="#p_FdzFEXmy5/" and tabindex or role aren't present, we need to map them with a part for the current page.
                                    anchor.Attributes.Add("kavita-page", $"{page}");
                                }
                                else
                                {
                                    anchor.Attributes.Add("target", "_blank");    
                                }
                                continue;
                            }
                                
                            var mappedPage = mappings[mappingKey];
                            anchor.Attributes.Add("kavita-page", $"{mappedPage}");
                            anchor.Attributes.Remove("href");
                            anchor.Attributes.Add("href", "javascript:void(0)");

                        }
                    }
                    
                    var images = doc.DocumentNode.SelectNodes("//img");
                    if (images != null)
                    {
                        foreach (var image in images)
                        {
                            if (image.Name != "img") continue;

                            var imageFile = image.Attributes["src"].Value;
                            image.Attributes.Remove("src");
                            image.Attributes.Add("src", $"{apiBase}" + imageFile);
                        }
                    }

                    return Ok(body.InnerHtml);
                }

                counter++;
            }

            return BadRequest("Could not find the appropriate html for that page");
        }

        private static bool HasClickableHrefPart(HtmlNode anchor)
        {
            return anchor.GetAttributeValue("href", string.Empty).Contains("#") 
                   && anchor.GetAttributeValue("tabindex", string.Empty) != "-1"
                   && anchor.GetAttributeValue("role", string.Empty) != "presentation";
        }

        private static string GetContentType(string fullFile)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fullFile, out var contentType))
            {
                var extension = Path.GetExtension(fullFile);
                if (extension == ".xhtml")
                {
                    contentType = "application/xhtml+xml";
                }
                else if (extension == ".xml" || extension == ".opf")
                {
                    contentType = "text/xml";
                }
                else
                {
                    contentType = "application/octet-stream";
                }
            }

            return contentType;
        }
    }
}