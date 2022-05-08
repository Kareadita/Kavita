﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Parser;
using Docnet.Core;
using Docnet.Core.Converters;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using ExCSS;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VersOne.Epub;
using VersOne.Epub.Options;
using Image = SixLabors.ImageSharp.Image;

namespace API.Services
{
    public interface IBookService
    {
        int GetNumberOfPages(string filePath);
        string GetCoverImage(string fileFilePath, string fileName, string outputDirectory);
        Task<Dictionary<string, int>> CreateKeyToPageMappingAsync(EpubBookRef book);

        /// <summary>
        /// Scopes styles to .reading-section and replaces img src to the passed apiBase
        /// </summary>
        /// <param name="stylesheetHtml"></param>
        /// <param name="apiBase"></param>
        /// <param name="filename">If the stylesheetHtml contains Import statements, when scoping the filename, scope needs to be wrt filepath.</param>
        /// <param name="book">Book Reference, needed for if you expect Import statements</param>
        /// <returns></returns>
        Task<string> ScopeStyles(string stylesheetHtml, string apiBase, string filename, EpubBookRef book);
        ComicInfo GetComicInfo(string filePath);
        ParserInfo ParseInfo(string filePath);
        /// <summary>
        /// Extracts a PDF file's pages as images to an target directory
        /// </summary>
        /// <param name="fileFilePath"></param>
        /// <param name="targetDirectory">Where the files will be extracted to. If doesn't exist, will be created.</param>
        void ExtractPdfImages(string fileFilePath, string targetDirectory);

        Task<string> ScopePage(HtmlDocument doc, EpubBookRef book, string apiBase, HtmlNode body, Dictionary<string, int> mappings, int page);
    }

    public class BookService : IBookService
    {
        private readonly ILogger<BookService> _logger;
        private readonly IDirectoryService _directoryService;
        private readonly IImageService _imageService;
        private readonly StylesheetParser _cssParser = new ();
        private static readonly RecyclableMemoryStreamManager StreamManager = new ();
        private const string CssScopeClass = ".book-content";
        public static readonly EpubReaderOptions BookReaderOptions = new()
        {
            PackageReaderOptions = new PackageReaderOptions()
            {
                IgnoreMissingToc = true
            }
        };

        public BookService(ILogger<BookService> logger, IDirectoryService directoryService, IImageService imageService)
        {
            _logger = logger;
            _directoryService = directoryService;
            _imageService = imageService;
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
            var prepend = filename.Length > 0 ? filename.Replace(Path.GetFileName(filename), string.Empty) : string.Empty;
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

            EscapeCssImportReferences(ref stylesheetHtml, apiBase, prepend);

            EscapeFontFamilyReferences(ref stylesheetHtml, apiBase, prepend);


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

        private static void EscapeCssImportReferences(ref string stylesheetHtml, string apiBase, string prepend)
        {
            foreach (Match match in Parser.Parser.CssImportUrlRegex.Matches(stylesheetHtml))
            {
                if (!match.Success) continue;
                var importFile = match.Groups["Filename"].Value;
                stylesheetHtml = stylesheetHtml.Replace(importFile, apiBase + prepend + importFile);
            }
        }

        private static void EscapeFontFamilyReferences(ref string stylesheetHtml, string apiBase, string prepend)
        {
            foreach (Match match in Parser.Parser.FontSrcUrlRegex.Matches(stylesheetHtml))
            {
                if (!match.Success) continue;
                var importFile = match.Groups["Filename"].Value;
                stylesheetHtml = stylesheetHtml.Replace(importFile, apiBase + prepend + importFile);
            }
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

        private static void ScopeImages(HtmlDocument doc, EpubBookRef book, string apiBase)
        {
            var images = doc.DocumentNode.SelectNodes("//img");
            if (images == null) return;

            foreach (var image in images)
            {
                if (image.Name != "img") continue;

                string key = null;
                if (image.Attributes["src"] != null)
                {
                    key = "src";
                }
                else if (image.Attributes["xlink:href"] != null)
                {
                    key = "xlink:href";
                }

                if (string.IsNullOrEmpty(key)) continue;

                var imageFile = GetKeyForImage(book, image.Attributes[key].Value);
                image.Attributes.Remove(key);
                image.Attributes.Add(key, $"{apiBase}" + imageFile);

                // Add a custom class that the reader uses to ensure images stay within reader
                image.AddClass("scale-width");
            }

        }

        /// <summary>
        /// Returns the image key associated with the file. Contains some basic fallback logic.
        /// </summary>
        /// <param name="book"></param>
        /// <param name="imageFile"></param>
        /// <returns></returns>
        private static string GetKeyForImage(EpubBookRef book, string imageFile)
        {
            if (!book.Content.Images.ContainsKey(imageFile))
            {
                var correctedKey = book.Content.Images.Keys.SingleOrDefault(s => s.EndsWith(imageFile));
                if (correctedKey != null)
                {
                    imageFile = correctedKey;
                }
                else if (imageFile.StartsWith(".."))
                {
                    // There are cases where the key is defined static like OEBPS/Images/1-4.jpg but reference is ../Images/1-4.jpg
                    correctedKey =
                        book.Content.Images.Keys.SingleOrDefault(s => s.EndsWith(imageFile.Replace("..", string.Empty)));
                    if (correctedKey != null)
                    {
                        imageFile = correctedKey;
                    }
                }
            }

            return imageFile;
        }

        private static string PrepareFinalHtml(HtmlDocument doc, HtmlNode body)
        {
            // Check if any classes on the html node (some r2l books do this) and move them to body tag for scoping
            var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
            if (htmlNode == null || !htmlNode.Attributes.Contains("class")) return body.InnerHtml;

            var bodyClasses = body.Attributes.Contains("class") ? body.Attributes["class"].Value : string.Empty;
            var classes = htmlNode.Attributes["class"].Value + " " + bodyClasses;
            body.Attributes.Add("class", $"{classes}");
            // I actually need the body tag itself for the classes, so i will create a div and put the body stuff there.
            return $"<div class=\"{body.Attributes["class"].Value}\">{body.InnerHtml}</div>";
        }

        private static void RewriteAnchors(int page, HtmlDocument doc, Dictionary<string, int> mappings)
        {
            var anchors = doc.DocumentNode.SelectNodes("//a");
            if (anchors != null)
            {
                foreach (var anchor in anchors)
                {
                    BookService.UpdateLinks(anchor, mappings, page);
                }
            }
        }

        private async Task InlineStyles(HtmlDocument doc, EpubBookRef book, string apiBase, HtmlNode body)
        {
            var inlineStyles = doc.DocumentNode.SelectNodes("//style");
            if (inlineStyles != null)
            {
                foreach (var inlineStyle in inlineStyles)
                {
                    var styleContent = await ScopeStyles(inlineStyle.InnerHtml, apiBase, "", book);
                    body.PrependChild(HtmlNode.CreateNode($"<style>{styleContent}</style>"));
                }
            }

            var styleNodes = doc.DocumentNode.SelectNodes("/html/head/link");
            if (styleNodes != null)
            {
                foreach (var styleLinks in styleNodes)
                {
                    var key = CleanContentKeys(styleLinks.Attributes["href"].Value);
                    // Some epubs are malformed the key in content.opf might be: content/resources/filelist_0_0.xml but the actual html links to resources/filelist_0_0.xml
                    // In this case, we will do a search for the key that ends with
                    if (!book.Content.Css.ContainsKey(key))
                    {
                        var correctedKey = book.Content.Css.Keys.SingleOrDefault(s => s.EndsWith(key));
                        if (correctedKey == null)
                        {
                            _logger.LogError("Epub is Malformed, key: {Key} is not matching OPF file", key);
                            continue;
                        }

                        key = correctedKey;
                    }

                    try
                    {
                        var cssFile = book.Content.Css[key];

                        var styleContent = await ScopeStyles(await cssFile.ReadContentAsync(), apiBase,
                            cssFile.FileName, book);
                        if (styleContent != null)
                        {
                            body.PrependChild(HtmlNode.CreateNode($"<style>{styleContent}</style>"));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "There was an error reading css file for inlining likely due to a key mismatch in metadata");
                    }
                }
            }
        }

        public ComicInfo GetComicInfo(string filePath)
        {
            if (!IsValidFile(filePath) || Parser.Parser.IsPdf(filePath)) return null;

            try
            {
                using var epubBook = EpubReader.OpenBook(filePath, BookReaderOptions);
                var publicationDate =
                    epubBook.Schema.Package.Metadata.Dates.FirstOrDefault(date => date.Event == "publication")?.Date;

                if (string.IsNullOrEmpty(publicationDate))
                {
                    publicationDate = epubBook.Schema.Package.Metadata.Dates.FirstOrDefault()?.Date;
                }
                var dateParsed = DateTime.TryParse(publicationDate, out var date);
                var year = 0;
                var month = 0;
                var day = 0;
                switch (dateParsed)
                {
                    case true:
                        year = date.Year;
                        month = date.Month;
                        day = date.Day;
                        break;
                    case false when !string.IsNullOrEmpty(publicationDate) && publicationDate.Length == 4:
                        int.TryParse(publicationDate, out year);
                        break;
                }

                var info =  new ComicInfo()
                {
                    Summary = epubBook.Schema.Package.Metadata.Description,
                    Writer = string.Join(",", epubBook.Schema.Package.Metadata.Creators.Select(c => Parser.Parser.CleanAuthor(c.Creator))),
                    Publisher = string.Join(",", epubBook.Schema.Package.Metadata.Publishers),
                    Month = month,
                    Day = day,
                    Year = year,
                    Title = epubBook.Title,
                    Genre = string.Join(",", epubBook.Schema.Package.Metadata.Subjects.Select(s => s.ToLower().Trim())),
                    LanguageISO = epubBook.Schema.Package.Metadata.Languages.FirstOrDefault() ?? string.Empty
                };
                ComicInfo.CleanComicInfo(info);

                // Parse tags not exposed via Library
                foreach (var metadataItem in epubBook.Schema.Package.Metadata.MetaItems)
                {
                    switch (metadataItem.Name)
                    {
                        case "calibre:rating":
                            info.UserRating = float.Parse(metadataItem.Content);
                            break;
                        case "calibre:title_sort":
                            info.TitleSort = metadataItem.Content;
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

               using var epubBook = EpubReader.OpenBook(filePath, BookReaderOptions);
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

        /// <summary>
        /// Removes all leading ../
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
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
                using var epubBook = EpubReader.OpenBook(filePath, BookReaderOptions);

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
                    var titleSort = string.Empty;


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
                                titleSort = metadataItem.Content;
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
                        var info = new ParserInfo()
                        {
                            Chapters = Parser.Parser.DefaultChapter,
                            Edition = string.Empty,
                            Format = MangaFormat.Epub,
                            Filename = Path.GetFileName(filePath),
                            Title = specialName?.Trim(),
                            FullFilePath = filePath,
                            IsSpecial = false,
                            Series = series.Trim(),
                            Volumes = seriesIndex
                        };

                        // Don't set titleSort if the book belongs to a group
                        if (!string.IsNullOrEmpty(titleSort) && string.IsNullOrEmpty(seriesIndex))
                        {
                            info.SeriesSort = titleSort;
                        }

                        return info;
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
                    Volumes = Parser.Parser.DefaultVolume,
                };
           }
           catch (Exception ex)
           {
              _logger.LogWarning(ex, "[BookService] There was an exception when opening epub book: {FileName}", filePath);
           }

           return null;
        }

        /// <summary>
        /// Extracts a pdf into images to a target directory. Uses multi-threaded implementation since docnet is slow normally.
        /// </summary>
        /// <param name="fileFilePath"></param>
        /// <param name="targetDirectory"></param>
        public void ExtractPdfImages(string fileFilePath, string targetDirectory)
        {
            _directoryService.ExistOrCreate(targetDirectory);

            using var docReader = DocLib.Instance.GetDocReader(fileFilePath, new PageDimensions(1080, 1920));
            var pages = docReader.GetPageCount();
            Parallel.For(0, pages, pageNumber =>
            {
                using var stream = StreamManager.GetStream("BookService.GetPdfPage");
                GetPdfPage(docReader, pageNumber, stream);
                using var fileStream = File.Create(Path.Combine(targetDirectory, "Page-" + pageNumber + ".png"));
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
            });
        }

        /// <summary>
        /// Responsible to scope all the css, links, tags, etc to prepare a self contained html file for the page
        /// </summary>
        /// <param name="doc">Html Doc that will be appended to</param>
        /// <param name="book">Underlying epub</param>
        /// <param name="apiBase">API Url for file loading to pass through</param>
        /// <param name="body">Body element from the epub</param>
        /// <param name="mappings">Epub mappings</param>
        /// <param name="page">Page number we are loading</param>
        /// <returns></returns>
        public async Task<string> ScopePage(HtmlDocument doc, EpubBookRef book, string apiBase, HtmlNode body, Dictionary<string, int> mappings, int page)
        {
            await InlineStyles(doc, book, apiBase, body);

            RewriteAnchors(page, doc, mappings);

            ScopeImages(doc, book, apiBase);

            return PrepareFinalHtml(doc, body);
        }

        /// <summary>
        /// Extracts the cover image to covers directory and returns file path back
        /// </summary>
        /// <param name="fileFilePath"></param>
        /// <param name="fileName">Name of the new file.</param>
        /// <returns></returns>
        public string GetCoverImage(string fileFilePath, string fileName, string outputDirectory)
        {
            if (!IsValidFile(fileFilePath)) return string.Empty;

            if (Parser.Parser.IsPdf(fileFilePath))
            {
                return GetPdfCoverImage(fileFilePath, fileName, outputDirectory);
            }

            using var epubBook = EpubReader.OpenBook(fileFilePath, BookReaderOptions);

            try
            {
                // Try to get the cover image from OPF file, if not set, try to parse it from all the files, then result to the first one.
                var coverImageContent = epubBook.Content.Cover
                                        ?? epubBook.Content.Images.Values.FirstOrDefault(file => Parser.Parser.IsCoverImage(file.FileName))
                                        ?? epubBook.Content.Images.Values.FirstOrDefault();

                if (coverImageContent == null) return string.Empty;
                using var stream = coverImageContent.GetContentStream();

                return _imageService.WriteCoverThumbnail(stream, fileName, outputDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[BookService] There was a critical error and prevented thumbnail generation on {BookFile}. Defaulting to no cover image", fileFilePath);
            }

            return string.Empty;
        }


        private string GetPdfCoverImage(string fileFilePath, string fileName, string outputDirectory)
        {
            try
            {
                using var docReader = DocLib.Instance.GetDocReader(fileFilePath, new PageDimensions(1080, 1920));
                if (docReader.GetPageCount() == 0) return string.Empty;

                using var stream = StreamManager.GetStream("BookService.GetPdfPage");
                GetPdfPage(docReader, 0, stream);

                return _imageService.WriteCoverThumbnail(stream, fileName, outputDirectory);

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
            var image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);

            stream.Seek(0, SeekOrigin.Begin);
            image.SaveAsPng(stream);
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
