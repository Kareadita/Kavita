using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using API.Entities.Interfaces;
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
            // TODO: Ensure the files have no ../ in it, so the API can't access anything outside of chapter folder.
            _logger.LogDebug("GetBookPageResources endpoint hit");
            var chapter = await _cacheService.Ensure(chapterId);
            
            // TODO: Apply link-rewriting here as well (especially for TOC)

            // NOTE: We need to use container.xml and opf to create the new paths

            var folder = file.Split("/");
            
            
            var fullFile = Path.Combine(Directory.GetCurrentDirectory(), "cache", chapterId + "", "OEBPS",
                folder[0], folder[1]);
            var contentType = GetContentType(fullFile);
            return PhysicalFile(fullFile, contentType);
        }

        [HttpGet("{chapterId}/chapters")]
        public async Task<ActionResult<Dictionary<string, int>>> GetBookChapters(int chapterId)
        {
            // This will return a list of mappings from ID -> pagenum. ID will be the xhtml key and pagenum will be the reading order
            // this is used to rewrite anchors in the book text so that we always load properly in FE
            var chapter = await _cacheService.Ensure(chapterId);
            var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath);
            var mappings = await _bookService.CreateKeyToPageMappingAsync(book, chapter.Files.ElementAt(0).FilePath);
            var dict = new Dictionary<string, int>();
            var navItems = await book.GetNavigationAsync();
            foreach (var navigationItem in navItems)
            {
                foreach (var nestedChapter in navigationItem.NestedItems)
                {
                    if (nestedChapter.Link == null) continue;
                    var key = BookService.CleanContentKeys(nestedChapter.Link.ContentFileName);
                    if (mappings.ContainsKey(key))
                    {
                        dict.Add(nestedChapter.Title, mappings[key]);    
                    }
                }
                
            }
            return Ok(dict);
        }



        [HttpGet("{chapterId}/book-page")]
        public async Task<ActionResult<string>> GetBookPage(int chapterId, [FromQuery] int page, [FromQuery] string baseUrl)
        {
            _logger.LogDebug("Book endpoint hit");
            var chapter = await _cacheService.Ensure(chapterId);

            var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath);
            var mappings = await _bookService.CreateKeyToPageMappingAsync(book, chapter.Files.ElementAt(0).FilePath);

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
                    
                    var styleNode = doc.DocumentNode.SelectSingleNode("/html/head/link");
                    if (styleNode != null)
                    {
                        var key = BookService.CleanContentKeys(styleNode.Attributes["href"].Value);
                        var styleContent = await book.Content.Css[key].ReadContentAsync();
                        body.PrependChild(HtmlNode.CreateNode($"<style>{BookService.RemoveWhiteSpaceFromStylesheets(styleContent)}</style>"));
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
                                anchor.Attributes.Add("target", "_blank");
                                continue;
                            }
                                
                            var mappedPage = mappings[mappingKey];
                            anchor.Attributes.Add("kavita-page", $"{mappedPage}");
                            anchor.Attributes.Remove("href");
                            anchor.Attributes.Add("href", "javascript:void(0)");
                        }
                    }
                        
                        
                    content = body.InnerHtml
                        .Replace("src=\"../", $"src=\"{apiBase}");
                    return Ok(content);
                }

                counter++;
            }

            return BadRequest("Could not find the appropriate html for that page");
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


        [HttpGet("{chapterId}/{file}")]
        public async Task<ActionResult> GetBookFile(int chapterId, string file)
        {
            var fullFile = Path.Combine(Directory.GetCurrentDirectory(), "cache", chapterId + "", file);
            var provider = new FileExtensionContentTypeProvider();
            if(!provider.TryGetContentType(fullFile, out var contentType))
            {
                var extension = Path.GetExtension(file);
                if (extension == ".xhtml")
                {
                    contentType = "application/xhtml+xml";
                } else if (extension == ".xml" || extension == ".opf")
                {
                    contentType = "text/xml";
                }
                else
                {
                    contentType = "application/octet-stream";    
                }

                contentType = "application/octet-stream";

            }
            
            return PhysicalFile(fullFile, contentType);
        }
    }
}