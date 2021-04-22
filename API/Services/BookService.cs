using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Archive;
using API.Controllers;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Extensions;
using API.Interfaces.Services;
using API.Parser;
using ExCSS;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using NetVips;
using SharpCompress.Archives;
using SharpCompress.Common;
using VersOne.Epub;

namespace API.Services
{
    public class BookService : IBookService
    {
        private readonly ILogger<BookService> _logger;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;

        private const int ThumbnailWidth = 320; // 153w x 230h
        //private readonly ObjectPool<EpubBook> _readerPool;
        private readonly StylesheetParser _cssParser = new ();
        

        public static readonly Regex StyleSheetKeyRegex = new Regex("href=\"(?<Key>[a-z0-9\\./#-]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public BookService(ILogger<BookService> logger, IArchiveService archiveService, IDirectoryService directoryService)
        {
            _logger = logger;
            _archiveService = archiveService;
            _directoryService = directoryService;
            //_readerPool = new DefaultObjectPool<EpubBook>();
            
        }
        
        private static bool HasClickableHrefPart(HtmlNode anchor)
        {
            return anchor.GetAttributeValue("href", string.Empty).Contains("#") 
                   && anchor.GetAttributeValue("tabindex", string.Empty) != "-1"
                   && anchor.GetAttributeValue("role", string.Empty) != "presentation";
        }

        public static string GetContentType(EpubContentType type)
        {
            string contentType;
            switch (type)
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

            return contentType;
        }

        public static void UpdateLinks(HtmlNode anchor, Dictionary<string, int> mappings, int currentPage)
        {
            if (anchor.Name != "a") return;
            var hrefParts = BookService.CleanContentKeys(anchor.GetAttributeValue("href", string.Empty))
                .Split("#");
            var mappingKey = hrefParts[0];
            if (!mappings.ContainsKey(mappingKey))
            {
                if (HasClickableHrefPart(anchor))
                {
                    var part = hrefParts.Length > 1
                        ? hrefParts[1]
                        : anchor.GetAttributeValue("href", string.Empty);
                    anchor.Attributes.Add("kavita-page", $"{currentPage}");
                    anchor.Attributes.Add("kavita-part", part);
                    anchor.Attributes.Remove("href");
                    anchor.Attributes.Add("href", "javascript:void(0)");
                }
                else
                {
                    anchor.Attributes.Add("target", "_blank");    
                }

                return;
            }
                                
            var mappedPage = mappings[mappingKey];
            anchor.Attributes.Add("kavita-page", $"{mappedPage}");
            if (hrefParts.Length > 1)
            {
                anchor.Attributes.Add("kavita-part",
                    hrefParts[1]);
            }
                            
            anchor.Attributes.Remove("href");
            anchor.Attributes.Add("href", "javascript:void(0)");
        }

        public async Task<string> ScopeStyles(string stylesheetHtml, string apiBase)
        {
            var styleContent = BookService.RemoveWhiteSpaceFromStylesheets(stylesheetHtml);
            styleContent =
                Parser.Parser.FontSrcUrlRegex.Replace(styleContent, "$1" + apiBase + "$2" + "$3");
            styleContent = styleContent.Replace("body", ".reading-section");
            
            var stylesheet = await _cssParser.ParseAsync(styleContent);
            foreach (var styleRule in stylesheet.StyleRules)
            {
                if (styleRule.Selector.Text == ".reading-section") continue;
                if (styleRule.Selector.Text.Contains(","))
                {
                    styleRule.Text = styleRule.Text.Replace(styleRule.SelectorText,
                        string.Join(", ",
                            styleRule.Selector.Text.Split(",").Select(s => ".reading-section " + s)));
                    continue;
                }
                styleRule.Text = ".reading-section " + styleRule.Text;
            }
            return RemoveWhiteSpaceFromStylesheets(stylesheet.ToCss());
        }

        private bool IsValidFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError("Book {ArchivePath} could not be found", filePath);
                return false;
            }

            if (Parser.Parser.IsBook(filePath)) return true;
            
            _logger.LogError("Book {ArchivePath} is not a valid EPUB", filePath);
            return false; 
        }

        public int GetNumberOfPages(string filePath)
        {
            if (!IsValidFile(filePath) || !Parser.Parser.IsEpub(filePath)) return 0;

            var epubBook = EpubReader.ReadBook(filePath);
            return epubBook.Content.Html.Count;
        }

        public static string CleanContentKeys(string key)
        {
            return key.Replace("../", string.Empty);
        }

        public async Task<Dictionary<string, int>> CreateKeyToPageMappingAsync(EpubBookRef book)
        {
            var dict = new Dictionary<string, int>();
            var pageCount = 0;
            foreach (var contentFileRef in await book.GetReadingOrderAsync())
            {
                if (contentFileRef.ContentType != EpubContentType.XHTML_1_1) continue;
                dict.Add(contentFileRef.FileName, pageCount);
                pageCount += 1;
            }
            
            return dict;
        }

        public ParserInfo ParseInfo(string filePath)
        {
            // TODO: Use a pool of EpubReaders since we are going to create these a lot
            var epubBook = EpubReader.ReadBook(filePath);
            
            return new ParserInfo()
            {
                Chapters = "0",
                Edition = "",
                Format = MangaFormat.Book,
                Filename = filePath,
                FullFilePath = filePath,
                IsSpecial = false,
                Series = epubBook.Title,
                Volumes = "0"
            };
        }

        public byte[] GetCoverImage(string fileFilePath, bool createThumbnail = true)
        {
            if (!IsValidFile(fileFilePath)) return Array.Empty<byte>();
            
            var epubBook = EpubReader.ReadBook(fileFilePath);
            
            
            try
            {
                // Try to get the cover image from OPF file, if not set, try to parse it from all the files, then result to the first one.
                var coverImageContent = epubBook.CoverImage 
                                        ?? epubBook.Content.Images.Values.FirstOrDefault(file => Parser.Parser.IsCoverImage(file.FileName))?.Content 
                                        ?? epubBook.Content.Images.Values.First().Content;

                if (coverImageContent != null && createThumbnail)
                {
                    using var stream = new MemoryStream(coverImageContent);

                    using var thumbnail = Image.ThumbnailStream(stream, ThumbnailWidth);
                    return thumbnail.WriteToBuffer(".jpg");
                }

                return coverImageContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was a critical error and prevented thumbnail generation on {BookFile}. Defaulting to no cover image", fileFilePath);
            }
            
            return Array.Empty<byte>();
        }

        public void ExtractToFolder(string archivePath, string extractPath)
        {
            if (!_archiveService.IsValidArchive(archivePath)) return;

            if (Directory.Exists(extractPath)) return;
            
            var sw = Stopwatch.StartNew();
            

            try
            {
                var libraryHandler = _archiveService.CanOpen(archivePath);
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        _logger.LogDebug("Using default compression handling");
                        using var archive = ZipFile.OpenRead(archivePath);
                        ExtractArchiveEntries(archive, extractPath);
                        break;
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        _logger.LogDebug("Using SharpCompress compression handling");
                        using var archive = ArchiveFactory.Open(archivePath);
                        ExtractArchiveEntities(archive.Entries, extractPath);
                        break;
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogError("[ExtractArchive] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return;
                    default:
                        _logger.LogError("[ExtractArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return;
                }
                
            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was a problem extracting {ArchivePath} to {ExtractPath}",archivePath, extractPath);
                return;
            }
            _logger.LogDebug("Extracted archive to {ExtractPath} in {ElapsedMilliseconds} milliseconds", extractPath, sw.ElapsedMilliseconds);
        }

        public static string RemoveWhiteSpaceFromStylesheets(string body)
        {
            body = Regex.Replace(body, @"[a-zA-Z]+#", "#");
            body = Regex.Replace(body, @"[\n\r]+\s*", string.Empty);
            body = Regex.Replace(body, @"\s+", " ");
            body = Regex.Replace(body, @"\s?([:,;{}])\s?", "$1");
            body = body.Replace(";}", "}");
            body = Regex.Replace(body, @"([\s:]0)(px|pt|%|em)", "$1");

            // Remove comments from CSS
            body = Regex.Replace(body, @"/\*[\d\D]*?\*/", string.Empty);

            return body;
        }

        private static void ExtractArchiveEntities(IEnumerable<IArchiveEntry> entries, string extractPath)
        {
            DirectoryService.ExistOrCreate(extractPath);
            foreach (var entry in entries)
            {
                entry.WriteToDirectory(extractPath, new ExtractionOptions()
                {
                    ExtractFullPath = false,
                    Overwrite = false
                });
            }
        }

        private void ExtractArchiveEntries(ZipArchive archive, string extractPath)
        {
            if (!archive.HasFiles()) return;
            archive.ExtractToDirectory(extractPath, true);
        }
    }
}