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
        
        private static readonly Regex StyleSheetKeyRegex = new Regex("href=\"(?<Key>[a-z0-9\\./]*)/\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            if (folder[1] == "toc.xhtml")
            {
                
            }
            
            var fullFile = Path.Combine(Directory.GetCurrentDirectory(), "cache", chapterId + "", "OEBPS",
                folder[0], folder[1]);
            var contentType = GetContentType(fullFile);
            return PhysicalFile(fullFile, contentType);
        }



        [HttpGet("{chapterId}/book-page")]
        public async Task<ActionResult<string>> GetBookPage(int chapterId, [FromQuery] int page)
        {
            _logger.LogDebug("Book endpoint hit");
            var chapter = await _cacheService.Ensure(chapterId);
            var host = Request.Host;
            
            var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath);
            var counter = 0;
            var doc = new HtmlDocument();
            // NOTE: We might want to remove toc links and put some marker for the UI to hook a click handler in (for drawer opening)
            // TODO: Figure out a less hacky way to accomplish this
            var apiBase = "http://" + Request.Host + "/api/book/" + chapterId + "/" + BookApiUrl;
            foreach (var contentFileRef in await book.GetReadingOrderAsync())
            {
                var content = await contentFileRef.ReadContentAsync();
                if (contentFileRef.ContentType == EpubContentType.XHTML_1_1)
                {
                    doc.LoadHtml(content);

                    var styleNode = doc.DocumentNode.SelectSingleNode("/html/head/link");
                    var key = styleNode.Attributes["href"].Value.Replace("../", "");
                    var styleContent = await book.Content.Css[key].ReadContentAsync();
                    var body = doc.DocumentNode.SelectSingleNode("/html/body");
                    body.PrependChild(HtmlNode.CreateNode($"<style>{BookService.RemoveWhiteSpaceFromStylesheets(styleContent)}</style>"));
                    content = body.InnerHtml
                                .Replace("src=\"../", $"src=\"{apiBase}")
                                .Replace("href=\"../", $"href=\"{apiBase}");
                }
                

                if (page == counter)
                {
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