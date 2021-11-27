using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Interfaces.Services;
using API.Parser;
using Docnet.Core;
using Docnet.Core.Converters;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using ExCSS;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using VersOne.Epub;

namespace API.Services
{
    public class BookService : IBookService
    {
        private readonly ILogger<BookService> _logger;
        private readonly StylesheetParser _cssParser = new ();
        private static readonly RecyclableMemoryStreamManager StreamManager = new ();
        private const string CssScopeClass = ".book-content";

        public BookService(ILogger<BookService> logger)
        {
            _logger = logger;

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
            var hrefParts = CleanContentKeys(anchor.GetAttributeValue("href", string.Empty))
                .Split("#");
            // Some keys get uri encoded when parsed, so replace any of those characters with original
            var mappingKey = HttpUtility.UrlDecode(hrefParts[0]);

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
                    anchor.Attributes.Add("rel", "noreferrer noopener");
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

        public async Task<string> ScopeStyles(string stylesheetHtml, string apiBase, string filename, EpubBookRef book)
        {
            // @Import statements will be handled by browser, so we must inline the css into the original file that request it, so they can be
            // Scoped
            var prepend = filename.Length > 0 ? filename.Replace(Path.GetFileName(filename), "") : string.Empty;
            var importBuilder = new StringBuilder();
            foreach (Match match in Parser.Parser.CssImportUrlRegex.Matches(stylesheetHtml))
            {
                if (!match.Success) continue;

                var importFile = match.Groups["Filename"].Value;
                var key = CleanContentKeys(importFile);
                if (!key.Contains(prepend))
                {
                    key = prepend + key;
                }
                if (!book.Content.AllFiles.ContainsKey(key)) continue;

                var bookFile = book.Content.AllFiles[key];
               var content = await bookFile.ReadContentAsBytesAsync();
               importBuilder.Append(Encoding.UTF8.GetString(content));
            }

            stylesheetHtml = stylesheetHtml.Insert(0, importBuilder.ToString());
            var importMatches = Parser.Parser.CssImportUrlRegex.Matches(stylesheetHtml);
            foreach (Match match in importMatches)
            {
                if (!match.Success) continue;
                var importFile = match.Groups["Filename"].Value;
                stylesheetHtml = stylesheetHtml.Replace(importFile, apiBase + prepend + importFile);
            }

            // Check if there are any background images and rewrite those urls
            EscapeCssImageReferences(ref stylesheetHtml, apiBase, book);

            var styleContent = RemoveWhiteSpaceFromStylesheets(stylesheetHtml);

            styleContent = styleContent.Replace("body", CssScopeClass);

            if (string.IsNullOrEmpty(styleContent)) return string.Empty;

            var stylesheet = await _cssParser.ParseAsync(styleContent);
            foreach (var styleRule in stylesheet.StyleRules)
            {
                if (styleRule.Selector.Text == CssScopeClass) continue;
                if (styleRule.Selector.Text.Contains(","))
                {
                    styleRule.Text = styleRule.Text.Replace(styleRule.SelectorText,
                        string.Join(", ",
                            styleRule.Selector.Text.Split(",").Select(s => $"{CssScopeClass} " + s)));
                    continue;
                }
                styleRule.Text = $"{CssScopeClass} " + styleRule.Text;
            }
            return RemoveWhiteSpaceFromStylesheets(stylesheet.ToCss());
        }

        private static void EscapeCssImageReferences(ref string stylesheetHtml, string apiBase, EpubBookRef book)
        {
            var matches = Parser.Parser.CssImageUrlRegex.Matches(stylesheetHtml);
            foreach (Match match in matches)
            {
                if (!match.Success) continue;

                var importFile = match.Groups["Filename"].Value;
                var key = CleanContentKeys(importFile);
                if (!book.Content.AllFiles.ContainsKey(key)) continue;

                stylesheetHtml = stylesheetHtml.Replace(importFile, apiBase + key);
            }
        }

        public ComicInfo GetComicInfo(string filePath)
        {
            if (!IsValidFile(filePath) || Parser.Parser.IsPdf(filePath)) return null;

            try
            {
                using var epubBook = EpubReader.OpenBook(filePath);
                var publicationDate =
                    epubBook.Schema.Package.Metadata.Dates.FirstOrDefault(date => date.Event == "publication")?.Date;

                var info =  new ComicInfo()
                {
                    Summary = epubBook.Schema.Package.Metadata.Description,
                    Writer = string.Join(",", epubBook.Schema.Package.Metadata.Creators),
                    Publisher = string.Join(",", epubBook.Schema.Package.Metadata.Publishers),
                    Month = !string.IsNullOrEmpty(publicationDate) ? DateTime.Parse(publicationDate).Month : 0,
                    Year = !string.IsNullOrEmpty(publicationDate) ? DateTime.Parse(publicationDate).Year : 0,
                };
                // Parse tags not exposed via Library
                foreach (var metadataItem in epubBook.Schema.Package.Metadata.MetaItems)
                {
                    switch (metadataItem.Name)
                    {
                        case "calibre:rating":
                            info.UserRating = float.Parse(metadataItem.Content);
                            break;
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GetComicInfo] There was an exception getting metadata");
            }

            return null;
        }

        private bool IsValidFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("[BookService] Book {EpubFile} could not be found", filePath);
                return false;
            }

            if (Parser.Parser.IsBook(filePath)) return true;

            _logger.LogWarning("[BookService] Book {EpubFile} is not a valid EPUB/PDF", filePath);
            return false;
        }

        public int GetNumberOfPages(string filePath)
        {
            if (!IsValidFile(filePath)) return 0;

            try
            {
               if (Parser.Parser.IsPdf(filePath))
               {
                   using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1080, 1920));
                   return docReader.GetPageCount();
               }

               using var epubBook = EpubReader.OpenBook(filePath);
               return epubBook.Content.Html.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[BookService] There was an exception getting number of pages, defaulting to 0");
            }

            return 0;
        }

        public static string EscapeTags(string content)
        {
            content = Regex.Replace(content, @"<script(.*)(/>)", "<script$1></script>");
            content = Regex.Replace(content, @"<title(.*)(/>)", "<title$1></title>");
            return content;
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

        /// <summary>
        /// Parses out Title from book. Chapters and Volumes will always be "0". If there is any exception reading book (malformed books)
        /// then null is returned. This expects only an epub file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public ParserInfo ParseInfo(string filePath)
        {
           if (!Parser.Parser.IsEpub(filePath)) return null;

           try
           {
                using var epubBook = EpubReader.OpenBook(filePath);

                // If the epub has the following tags, we can group the books as Volumes
                // <meta content="5.0" name="calibre:series_index"/>
                // <meta content="The Dark Tower" name="calibre:series"/>
                // <meta content="Wolves of the Calla" name="calibre:title_sort"/>
                // If all three are present, we can take that over dc:title and format as:
                // Series = The Dark Tower, Volume = 5, Filename as "Wolves of the Calla"
                // In addition, the following can exist and should parse as a series (EPUB 3.2 spec)
                // <meta property="belongs-to-collection" id="c01">
                //   The Lord of the Rings
                // </meta>
                // <meta refines="#c01" property="collection-type">set</meta>
                // <meta refines="#c01" property="group-position">2</meta>
                try
                {
                    var seriesIndex = string.Empty;
                    var series = string.Empty;
                    var specialName = string.Empty;
                    var groupPosition = string.Empty;


                    foreach (var metadataItem in epubBook.Schema.Package.Metadata.MetaItems)
                    {
                        // EPUB 2 and 3
                        switch (metadataItem.Name)
                        {
                            case "calibre:series_index":
                                seriesIndex = metadataItem.Content;
                                break;
                            case "calibre:series":
                                series = metadataItem.Content;
                                break;
                            case "calibre:title_sort":
                                specialName = metadataItem.Content;
                                break;
                        }

                        // EPUB 3.2+ only
                        switch (metadataItem.Property)
                        {
                            case "group-position":
                                seriesIndex = metadataItem.Content;
                                break;
                            case "belongs-to-collection":
                                series = metadataItem.Content;
                                break;
                            case "collection-type":
                                groupPosition = metadataItem.Content;
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(series) && !string.IsNullOrEmpty(seriesIndex) &&
                        (!string.IsNullOrEmpty(specialName) || groupPosition.Equals("series") || groupPosition.Equals("set")))
                    {
                        if (string.IsNullOrEmpty(specialName))
                        {
                            specialName = epubBook.Title;
                        }
                        return new ParserInfo()
                        {
                            Chapters = Parser.Parser.DefaultChapter,
                            Edition = string.Empty,
                            Format = MangaFormat.Epub,
                            Filename = Path.GetFileName(filePath),
                            Title = specialName.Trim(),
                            FullFilePath = filePath,
                            IsSpecial = false,
                            Series = series.Trim(),
                            Volumes = seriesIndex
                        };
                    }
                }
                catch (Exception)
                {
                    // Swallow exception
                }

                return new ParserInfo()
                {
                    Chapters = Parser.Parser.DefaultChapter,
                    Edition = string.Empty,
                    Format = MangaFormat.Epub,
                    Filename = Path.GetFileName(filePath),
                    Title = epubBook.Title.Trim(),
                    FullFilePath = filePath,
                    IsSpecial = false,
                    Series = epubBook.Title.Trim(),
                    Volumes = Parser.Parser.DefaultVolume
                };
           }
           catch (Exception ex)
           {
              _logger.LogWarning(ex, "[BookService] There was an exception when opening epub book: {FileName}", filePath);
           }

           return null;
        }

        private static void AddBytesToBitmap(Bitmap bmp, byte[] rawBytes)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);

            var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
            var pNative = bmpData.Scan0;

            Marshal.Copy(rawBytes, 0, pNative, rawBytes.Length);
            bmp.UnlockBits(bmpData);
        }

        public void ExtractPdfImages(string fileFilePath, string targetDirectory)
        {
            DirectoryService.ExistOrCreate(targetDirectory);

            using var docReader = DocLib.Instance.GetDocReader(fileFilePath, new PageDimensions(1080, 1920));
            var pages = docReader.GetPageCount();
            using var stream = StreamManager.GetStream("BookService.GetPdfPage");
            for (var pageNumber = 0; pageNumber < pages; pageNumber++)
            {
                GetPdfPage(docReader, pageNumber, stream);
                using var fileStream = File.Create(Path.Combine(targetDirectory, "Page-" + pageNumber + ".png"));
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
            }
        }

        /// <summary>
        /// Extracts the cover image to covers directory and returns file path back
        /// </summary>
        /// <param name="fileFilePath"></param>
        /// <param name="fileName">Name of the new file.</param>
        /// <returns></returns>
        public string GetCoverImage(string fileFilePath, string fileName)
        {
            if (!IsValidFile(fileFilePath)) return string.Empty;

            if (Parser.Parser.IsPdf(fileFilePath))
            {
                return GetPdfCoverImage(fileFilePath, fileName);
            }

            using var epubBook = EpubReader.OpenBook(fileFilePath);


            try
            {
                // Try to get the cover image from OPF file, if not set, try to parse it from all the files, then result to the first one.
                var coverImageContent = epubBook.Content.Cover
                                        ?? epubBook.Content.Images.Values.FirstOrDefault(file => Parser.Parser.IsCoverImage(file.FileName))
                                        ?? epubBook.Content.Images.Values.FirstOrDefault();

                if (coverImageContent == null) return string.Empty;
                using var stream = coverImageContent.GetContentStream();

                return ImageService.WriteCoverThumbnail(stream, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[BookService] There was a critical error and prevented thumbnail generation on {BookFile}. Defaulting to no cover image", fileFilePath);
            }

            return string.Empty;
        }


        private string GetPdfCoverImage(string fileFilePath, string fileName)
        {
            try
            {
                using var docReader = DocLib.Instance.GetDocReader(fileFilePath, new PageDimensions(1080, 1920));
                if (docReader.GetPageCount() == 0) return string.Empty;

                using var stream = StreamManager.GetStream("BookService.GetPdfPage");
                GetPdfPage(docReader, 0, stream);

                return ImageService.WriteCoverThumbnail(stream, fileName);

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[BookService] There was a critical error and prevented thumbnail generation on {BookFile}. Defaulting to no cover image",
                    fileFilePath);
            }

            return string.Empty;
        }

        private static void GetPdfPage(IDocReader docReader, int pageNumber, Stream stream)
        {
            using var pageReader = docReader.GetPageReader(pageNumber);
            var rawBytes = pageReader.GetImage(new NaiveTransparencyRemover());
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            AddBytesToBitmap(bmp, rawBytes);
            // Removes 1px margin on left/right side after bitmap is copied out
            for (var y = 0; y < bmp.Height; y++)
            {
                bmp.SetPixel(bmp.Width - 1, y, bmp.GetPixel(bmp.Width - 2, y));
            }
            stream.Seek(0, SeekOrigin.Begin);
            bmp.Save(stream, ImageFormat.Jpeg);
            stream.Seek(0, SeekOrigin.Begin);
        }

        private static string RemoveWhiteSpaceFromStylesheets(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return string.Empty;
            }

            // Remove comments from CSS
            body = Regex.Replace(body, @"/\*[\d\D]*?\*/", string.Empty);

            body = Regex.Replace(body, @"[a-zA-Z]+#", "#");
            body = Regex.Replace(body, @"[\n\r]+\s*", string.Empty);
            body = Regex.Replace(body, @"\s+", " ");
            body = Regex.Replace(body, @"\s?([:,;{}])\s?", "$1");
            try
            {
                body = body.Replace(";}", "}");
            }
            catch (Exception)
            {
                /* Swallow exception. Some css doesn't have style rules ending in ; */
            }

            body = Regex.Replace(body, @"([\s:]0)(px|pt|%|em)", "$1");


            return body;
        }
    }
}
