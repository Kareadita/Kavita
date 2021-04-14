using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using API.Entities.Interfaces;
using API.Interfaces;
using API.Interfaces.Services;
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
            _logger.LogDebug("GetBookPageResources endpoint hit");
            var chapter = await _cacheService.Ensure(chapterId);

            var folder = file.Split("/");
            
            var fullFile = Path.Combine(Directory.GetCurrentDirectory(), "cache", chapterId + "", "OEBPS",
                folder[0], folder[1]);
            var contentType = GetContentType(fullFile);
            return PhysicalFile(fullFile, contentType);
            
            return Ok();
        }



        [HttpGet("{chapterId}/book-page")]
        public async Task<ActionResult> GetBookPage(int chapterId, [FromQuery] int page)
        {
            _logger.LogDebug("Book endpoint hit");
            // First POC: Generate a mapping between page Num and the file
            // Send the file back as html and let user render it
            var chapter = await _cacheService.Ensure(chapterId);
            
            //var (path, _) = await _cacheService.GetCachedPagePath(chapter, page);
            
            //if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"No such html for page {page}");
            var pageName = "titlepage.xhtml";
            var api = "book-resources?file=";

            var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath);
            var counter = 0;
            foreach (var contentFileRef in book.GetReadingOrder())
            {
                var content = await contentFileRef.ReadContentAsync();
                if (contentFileRef.ContentType == EpubContentType.XHTML_1_1)
                {
                    content = content.Replace("src=\"../", $"src=\"{api}").Replace("href=\"../", $"href=\"{api}");    
                }
                

                if (page == counter)
                {
                    return new FileContentResult(Encoding.ASCII.GetBytes(content), "text/html");
                }

                counter++;
            }

            var fullFile = Path.Combine(Directory.GetCurrentDirectory(), "cache", chapterId + "", "OEBPS",
                "Text", pageName);
            var contentType = GetContentType(fullFile);
            return PhysicalFile(fullFile, contentType);
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