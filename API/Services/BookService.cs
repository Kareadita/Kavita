using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using API.Data.Metadata;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;
using API.Helpers;
using Docnet.Core;
using Docnet.Core.Converters;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using ExCSS;
using HtmlAgilityPack;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Nager.ArticleNumber;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VersOne.Epub;
using VersOne.Epub.Options;
using VersOne.Epub.Schema;

namespace API.Services;

#nullable enable

public interface IBookService
{
    int GetNumberOfPages(string filePath);
    string GetCoverImage(string fileFilePath, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
    ComicInfo? GetComicInfo(string filePath);
    ParserInfo? ParseInfo(string filePath);
    /// <summary>
    /// Scopes styles to .reading-section and replaces img src to the passed apiBase
    /// </summary>
    /// <param name="stylesheetHtml"></param>
    /// <param name="apiBase"></param>
    /// <param name="filename">If the stylesheetHtml contains Import statements, when scoping the filename, scope needs to be wrt filepath.</param>
    /// <param name="book">Book Reference, needed for if you expect Import statements</param>
    /// <returns></returns>
    Task<string> ScopeStyles(string stylesheetHtml, string apiBase, string filename, EpubBookRef book);
    /// <summary>
    /// Extracts a PDF file's pages as images to an target directory
    /// </summary>
    /// <remarks>This method relies on Docnet which has explicit patches from Kavita for ARM support. This should only be used with Tachiyomi</remarks>
    /// <param name="fileFilePath"></param>
    /// <param name="targetDirectory">Where the files will be extracted to. If doesn't exist, will be created.</param>
    void ExtractPdfImages(string fileFilePath, string targetDirectory);
    Task<ICollection<BookChapterItem>> GenerateTableOfContents(Chapter chapter);
    Task<string> GetBookPage(int page, int chapterId, string cachedEpubPath, string baseUrl);
    Task<Dictionary<string, int>> CreateKeyToPageMappingAsync(EpubBookRef book);
}

public class BookService : IBookService
{
    private readonly ILogger<BookService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IImageService _imageService;
    private readonly IMediaErrorService _mediaErrorService;
    private readonly StylesheetParser _cssParser = new ();
    private static readonly RecyclableMemoryStreamManager StreamManager = new ();
    private const string CssScopeClass = ".book-content";
    private const string BookApiUrl = "book-resources?file=";
    private readonly PdfComicInfoExtractor _pdfComicInfoExtractor;

    public static readonly EpubReaderOptions BookReaderOptions = new()
    {
        PackageReaderOptions = new PackageReaderOptions
        {
            IgnoreMissingToc = true,
            SkipInvalidManifestItems = true
        }
    };

    public BookService(ILogger<BookService> logger, IDirectoryService directoryService, IImageService imageService, IMediaErrorService mediaErrorService)
    {
        _logger = logger;
        _directoryService = directoryService;
        _imageService = imageService;
        _mediaErrorService = mediaErrorService;
        _pdfComicInfoExtractor = new PdfComicInfoExtractor(_logger, _mediaErrorService);
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

    private static void UpdateLinks(HtmlNode anchor, Dictionary<string, int> mappings, int currentPage)
    {
        if (anchor.Name != "a") return;
        var hrefParts = CleanContentKeys(anchor.GetAttributeValue("href", string.Empty))
            .Split("#");
        // Some keys get uri encoded when parsed, so replace any of those characters with original
        var mappingKey = Uri.UnescapeDataString(hrefParts[0]);

        if (!mappings.ContainsKey(mappingKey))
        {
            if (HasClickableHrefPart(anchor))
            {
                var part = hrefParts.Length > 1
                    ? hrefParts[1]
                    : anchor.GetAttributeValue("href", string.Empty);

                // hrefParts[0] might not have path from mappings
                var pageKey = mappings.Keys.FirstOrDefault(mKey => mKey.EndsWith(hrefParts[0]));
                if (!string.IsNullOrEmpty(pageKey))
                {
                    mappings.TryGetValue(pageKey, out currentPage);
                }


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

    /// <summary>
    /// Scopes styles to .reading-section and replaces img src to the passed apiBase
    /// </summary>
    /// <param name="stylesheetHtml"></param>
    /// <param name="apiBase"></param>
    /// <param name="filename">If the stylesheetHtml contains Import statements, when scoping the filename, scope needs to be wrt filepath.</param>
    /// <param name="book">Book Reference, needed for if you expect Import statements</param>
    /// <returns></returns>
    public async Task<string> ScopeStyles(string stylesheetHtml, string apiBase, string filename, EpubBookRef book)
    {
        // @Import statements will be handled by browser, so we must inline the css into the original file that request it, so they can be Scoped
        var prepend = filename.Length > 0 ? filename.Replace(Path.GetFileName(filename), string.Empty) : string.Empty;
        var importBuilder = new StringBuilder();

        foreach (Match match in Parser.CssImportUrlRegex.Matches(stylesheetHtml))
        {
            if (!match.Success) continue;

            var importFile = match.Groups["Filename"].Value;
            var key = CleanContentKeys(importFile); // Validate if CoalesceKey works well here
            if (!key.Contains(prepend))
            {
                key = prepend + key;
            }
            if (!book.Content.AllFiles.TryGetLocalFileRefByKey(key, out var bookFile)) continue;

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
            if (styleRule.Selector.Text.Contains(','))
            {
                styleRule.Text = styleRule.Text.Replace(styleRule.SelectorText,
                    string.Join(", ",
                        styleRule.Selector.Text.Split(",").Select(s => $"{CssScopeClass} " + s)));
                continue;
            }
            styleRule.Text = $"{CssScopeClass} " + styleRule.Text;
        }

        try
        {
            return RemoveWhiteSpaceFromStylesheets(stylesheet.ToCss());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue escaping css, likely due to an unsupported css rule");
        }
        return RemoveWhiteSpaceFromStylesheets($"{CssScopeClass} {styleContent}");
    }

    private static void EscapeCssImportReferences(ref string stylesheetHtml, string apiBase, string prepend)
    {
        foreach (Match match in Parser.CssImportUrlRegex.Matches(stylesheetHtml))
        {
            if (!match.Success) continue;
            var importFile = match.Groups["Filename"].Value;
            stylesheetHtml = stylesheetHtml.Replace(importFile, apiBase + prepend + importFile);
        }
    }

    private static void EscapeFontFamilyReferences(ref string stylesheetHtml, string apiBase, string prepend)
    {
        foreach (Match match in Parser.FontSrcUrlRegex.Matches(stylesheetHtml))
        {
            if (!match.Success) continue;
            var importFile = match.Groups["Filename"].Value;
            stylesheetHtml = stylesheetHtml.Replace(importFile, apiBase + prepend + importFile);
        }
    }

    private static void EscapeCssImageReferences(ref string stylesheetHtml, string apiBase, EpubBookRef book)
    {
        var matches = Parser.CssImageUrlRegex.Matches(stylesheetHtml);
        foreach (Match match in matches)
        {
            if (!match.Success) continue;

            var importFile = match.Groups["Filename"].Value;
            var key = CleanContentKeys(importFile);
            if (!book.Content.AllFiles.ContainsLocalFileRefWithKey(key)) continue;

            stylesheetHtml = stylesheetHtml.Replace(importFile, apiBase + key);
        }
    }

    private static void ScopeImages(HtmlDocument doc, EpubBookRef book, string apiBase)
    {
        var images = doc.DocumentNode.SelectNodes("//img")
                     ?? doc.DocumentNode.SelectNodes("//image") ?? doc.DocumentNode.SelectNodes("//svg");

        if (images == null) return;

        var parent = images[0].ParentNode;

        foreach (var image in images)
        {

            string? key = null;
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
            // UrlEncode here to transform ../ into an escaped version, which avoids blocking on nginx
            image.Attributes.Add(key, $"{apiBase}" + Uri.EscapeDataString(imageFile));

            // Add a custom class that the reader uses to ensure images stay within reader
            parent.AddClass("kavita-scale-width-container");
            image.AddClass("kavita-scale-width");
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
        if (book.Content.Images.ContainsLocalFileRefWithKey(imageFile)) return imageFile;

        var correctedKey = book.Content.Images.Local.Select(s => s.Key).SingleOrDefault(s => s.EndsWith(imageFile));
        if (correctedKey != null)
        {
            imageFile = correctedKey;
        }
        else if (imageFile.StartsWith(".."))
        {
            // There are cases where the key is defined static like OEBPS/Images/1-4.jpg but reference is ../Images/1-4.jpg
            correctedKey =
                book.Content.Images.Local.Select(s => s.Key).SingleOrDefault(s => s.EndsWith(imageFile.Replace("..", string.Empty)));
            if (correctedKey != null)
            {
                imageFile = correctedKey;
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
        if (anchors == null) return;

        foreach (var anchor in anchors)
        {
            UpdateLinks(anchor, mappings, page);
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

        var styleNodes = doc.DocumentNode.SelectNodes("/html/head/link[@href]");
        if (styleNodes != null)
        {
            foreach (var styleLinks in styleNodes)
            {
                var key = CleanContentKeys(styleLinks.Attributes["href"].Value);
                // Some epubs are malformed the key in content.opf might be: content/resources/filelist_0_0.xml but the actual html links to resources/filelist_0_0.xml
                // In this case, we will do a search for the key that ends with
                if (!book.Content.Css.ContainsLocalFileRefWithKey(key))
                {
                    var correctedKey = book.Content.Css.Local.Select(s => s.Key).SingleOrDefault(s => s.EndsWith(key));
                    if (correctedKey == null)
                    {
                        _logger.LogError("Epub is Malformed, key: {Key} is not matching OPF file", key);
                        continue;
                    }

                    key = correctedKey;
                }

                try
                {
                    var cssFile = book.Content.Css.GetLocalFileRefByKey(key);

                    var stylesheetHtml = await cssFile.ReadContentAsync();
                    var styleContent = await ScopeStyles(stylesheetHtml, apiBase,
                        cssFile.FilePath, book);
                    if (styleContent != null)
                    {
                        body.PrependChild(HtmlNode.CreateNode($"<style>{styleContent}</style>"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "There was an error reading css file for inlining likely due to a key mismatch in metadata");
                    await _mediaErrorService.ReportMediaIssueAsync(book.FilePath, MediaErrorProducer.BookService,
                        "There was an error reading css file for inlining likely due to a key mismatch in metadata", ex);
                }
            }
        }
    }

    private ComicInfo? GetEpubComicInfo(string filePath)
    {
        try
        {
            using var epubBook = EpubReader.OpenBook(filePath, BookReaderOptions);
            var publicationDate =
                epubBook.Schema.Package.Metadata.Dates.Find(pDate => pDate.Event == "publication")?.Date;

            if (string.IsNullOrEmpty(publicationDate))
            {
                publicationDate = epubBook.Schema.Package.Metadata.Dates.FirstOrDefault()?.Date;
            }
            var (year, month, day) = GetPublicationDate(publicationDate);

            var summary = epubBook.Schema.Package.Metadata.Descriptions.FirstOrDefault();
            var info = new ComicInfo
            {
                Summary = string.IsNullOrEmpty(summary?.Description) ? string.Empty : summary.Description,
                Publisher = string.Join(",", epubBook.Schema.Package.Metadata.Publishers.Select(p => p.Publisher)),
                Month = month,
                Day = day,
                Year = year,
                Title = epubBook.Title,
                Genre = string.Join(",", epubBook.Schema.Package.Metadata.Subjects.Select(s => s.Subject.ToLower().Trim())),
                LanguageISO = ValidateLanguage(epubBook.Schema.Package.Metadata.Languages
                    .Select(l => l.Language)
                    .FirstOrDefault())
            };
            ComicInfo.CleanComicInfo(info);

            var weblinks = new List<string>();
            foreach (var identifier in epubBook.Schema.Package.Metadata.Identifiers)
            {
                if (string.IsNullOrEmpty(identifier.Identifier)) continue;
                if (!string.IsNullOrEmpty(identifier.Scheme) && identifier.Scheme.Equals("ISBN", StringComparison.InvariantCultureIgnoreCase))
                {
                    var isbn = identifier.Identifier.Replace("urn:isbn:", string.Empty).Replace("isbn:", string.Empty);
                    if (!ArticleNumberHelper.IsValidIsbn10(isbn) && !ArticleNumberHelper.IsValidIsbn13(isbn))
                    {
                        _logger.LogDebug("[BookService] {File} has invalid ISBN number", filePath);
                        continue;
                    }
                    info.Isbn = isbn;
                }

                if ((!string.IsNullOrEmpty(identifier.Scheme) && identifier.Scheme.Equals("URL", StringComparison.InvariantCultureIgnoreCase)) ||
                     identifier.Identifier.StartsWith("url:"))
                {
                    var url = identifier.Identifier.Replace("url:", string.Empty);
                    weblinks.Add(url.Trim());
                }
            }

            if (weblinks.Count > 0)
            {
                info.Web = string.Join(',', weblinks.Distinct());
            }

            // Parse tags not exposed via Library
            foreach (var metadataItem in epubBook.Schema.Package.Metadata.MetaItems)
            {
                // EPUB 2 and 3
                switch (metadataItem.Name)
                {
                    case "calibre:rating":
                        info.UserRating = metadataItem.Content.AsFloat();
                        break;
                    case "calibre:title_sort":
                        info.TitleSort = metadataItem.Content;
                        break;
                    case "calibre:series":
                        info.Series = metadataItem.Content;
                        if (string.IsNullOrEmpty(info.SeriesSort))
                        {
                            info.SeriesSort = metadataItem.Content;
                        }
                        break;
                    case "calibre:series_index":
                        info.Volume = metadataItem.Content;
                        break;
                }


                // EPUB 3.2+ only
                switch (metadataItem.Property)
                {
                    case "group-position":
                        info.Volume = metadataItem.Content;
                        break;
                    case "belongs-to-collection":
                        info.Series = metadataItem.Content;
                        if (string.IsNullOrEmpty(info.SeriesSort))
                        {
                            info.SeriesSort = metadataItem.Content;
                        }
                        break;
                    case "collection-type":
                        // These look to be genres from https://manual.calibre-ebook.com/sub_groups.html or can be "series"
                        break;
                    case "role":
                        if (metadataItem.Scheme != null && !metadataItem.Scheme.Equals("marc:relators")) break;

                        var creatorId = metadataItem.Refines?.Replace("#", string.Empty);
                        var person = epubBook.Schema.Package.Metadata.Creators
                            .SingleOrDefault(c => c.Id == creatorId);
                        if (person == null) break;

                        PopulatePerson(metadataItem, info, person);
                        break;
                    case "title-type":
                        if (metadataItem.Content.Equals("collection"))
                        {
                            ExtractCollectionOrReadingList(metadataItem, epubBook, info);
                        }

                        if (metadataItem.Content.Equals("main"))
                        {
                            ExtractSortTitle(metadataItem, epubBook, info);
                        }

                        break;
                }
            }

            // If this is a single book and not a collection, set publication status to Completed
            if (string.IsNullOrEmpty(info.Volume) && Parser.ParseVolume(filePath, LibraryType.Manga).Equals(Parser.LooseLeafVolume))
            {
                info.Count = 1;
            }

            // Include regular Writer as well, for cases where there is no special tag
            info.Writer = string.Join(",",
                epubBook.Schema.Package.Metadata.Creators.Select(c => Parser.CleanAuthor(c.Creator)));

            var hasVolumeInSeries = !Parser.ParseVolume(info.Title, LibraryType.Manga)
                .Equals(Parser.LooseLeafVolume);

            if (string.IsNullOrEmpty(info.Volume) && hasVolumeInSeries && (!info.Series.Equals(info.Title) || string.IsNullOrEmpty(info.Series)))
            {
                // This is likely a light novel for which we can set series from parsed title
                info.Series = Parser.ParseSeries(info.Title, LibraryType.Manga);
                info.Volume = Parser.ParseVolume(info.Title, LibraryType.Manga);
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetComicInfo] There was an exception parsing metadata");
            _mediaErrorService.ReportMediaIssue(filePath, MediaErrorProducer.BookService,
                "There was an exception parsing metadata", ex);
        }

        return null;
    }

    public ComicInfo? GetComicInfo(string filePath)
    {
        if (!IsValidFile(filePath)) return null;

        if (Parser.IsPdf(filePath))
        {
            return _pdfComicInfoExtractor.GetComicInfo(filePath);
        }
        else
        {
            return GetEpubComicInfo(filePath);
        }
    }

    private static void ExtractSortTitle(EpubMetadataMeta metadataItem, EpubBookRef epubBook, ComicInfo info)
    {
        var titleId = metadataItem.Refines?.Replace("#", string.Empty);
        var titleElem = epubBook.Schema.Package.Metadata.Titles
            .Find(item => item.Id == titleId);
        if (titleElem == null) return;

        var sortTitleElem = epubBook.Schema.Package.Metadata.MetaItems
            .Find(item =>
                item.Property == "file-as" && item.Refines == metadataItem.Refines);
        if (sortTitleElem == null || string.IsNullOrWhiteSpace(sortTitleElem.Content)) return;
        info.SeriesSort = sortTitleElem.Content;
    }

    private static void ExtractCollectionOrReadingList(EpubMetadataMeta metadataItem, EpubBookRef epubBook, ComicInfo info)
    {
        var titleId = metadataItem.Refines?.Replace("#", string.Empty);
        var readingListElem = epubBook.Schema.Package.Metadata.Titles
            .Find(item => item.Id == titleId);
        if (readingListElem == null) return;

        var count = epubBook.Schema.Package.Metadata.MetaItems
            .Find(item =>
                item.Property == "display-seq" && item.Refines == metadataItem.Refines);
        if (count == null || count.Content == "0")
        {
            // Treat this as a Collection
            info.SeriesGroup += (string.IsNullOrEmpty(info.StoryArc) ? string.Empty : ",") +
                                readingListElem.Title.Replace(',', '_');
        }
        else
        {
            // Treat as a reading list
            info.AlternateSeries += (string.IsNullOrEmpty(info.AlternateSeries) ? string.Empty : ",") +
                                    readingListElem.Title.Replace(',', '_');
            info.AlternateNumber += (string.IsNullOrEmpty(info.AlternateNumber) ? string.Empty : ",") + count.Content;
        }
    }

    private static void PopulatePerson(EpubMetadataMeta metadataItem, ComicInfo info, EpubMetadataCreator person)
    {
        switch (metadataItem.Content)
        {
            case "art":
            case "artist":
                info.CoverArtist += AppendAuthor(person);
                return;
            case "aut":
            case "author":
            case "creator":
            case "cre":
                info.Writer += AppendAuthor(person);
                return;
            case "pbl":
            case "publisher":
                info.Publisher += AppendAuthor(person);
                return;
            case "trl":
            case "translator":
                info.Translator += AppendAuthor(person);
                return;
            case "edt":
            case "editor":
                info.Editor += AppendAuthor(person);
                return;
            case "ill":
            case "illustrator":
                info.Inker += AppendAuthor(person);
                return;
            case "clr":
            case "colorist":
                info.Colorist += AppendAuthor(person);
                return;
        }
    }

    private static string AppendAuthor(EpubMetadataCreator person)
    {
        return Parser.CleanAuthor(person.Creator) + ",";
    }

    private static (int year, int month, int day) GetPublicationDate(string? publicationDate)
    {
        var year = 0;
        var month = 0;
        var day = 0;
        if (string.IsNullOrEmpty(publicationDate)) return (year, month, day);
        switch (DateTime.TryParse(publicationDate, out var date))
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

        return (year, month, day);
    }

    public static string ValidateLanguage(string? language)
    {
        if (string.IsNullOrEmpty(language)) return string.Empty;

        try
        {
            return CultureInfo.GetCultureInfo(language).ToString();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private bool IsValidFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("[BookService] Book {EpubFile} could not be found", filePath);
            return false;
        }

        if (Parser.IsBook(filePath)) return true;

        _logger.LogWarning("[BookService] Book {EpubFile} is not a valid EPUB/PDF", filePath);
        return false;
    }

    public int GetNumberOfPages(string filePath)
    {
        if (!IsValidFile(filePath)) return 0;

        try
        {
            if (Parser.IsPdf(filePath))
            {
                using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1080, 1920));
                return docReader.GetPageCount();
            }

            using var epubBook = EpubReader.OpenBook(filePath, BookReaderOptions);
            return epubBook.GetReadingOrder().Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BookService] There was an exception getting number of pages, defaulting to 0");
            _mediaErrorService.ReportMediaIssue(filePath, MediaErrorProducer.BookService,
                "There was an exception getting number of pages, defaulting to 0", ex);
        }

        return 0;
    }

    private static string EscapeTags(string content)
    {
        content = Regex.Replace(content, @"<script(.*)(/>)", "<script$1></script>", RegexOptions.None, Parser.RegexTimeout);
        content = Regex.Replace(content, @"<title(.*)(/>)", "<title$1></title>", RegexOptions.None, Parser.RegexTimeout);
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
            // Some keys are different than FilePath, so we add both to ease loookup
            dict.Add(contentFileRef.FilePath, pageCount); // FileName -> FilePath
            dict.TryAdd(contentFileRef.Key, pageCount); // FileName -> FilePath
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
    public ParserInfo? ParseInfo(string filePath)
    {
        if (!Parser.IsEpub(filePath) || !_directoryService.FileSystem.File.Exists(filePath)) return null;

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
                            // These look to be genres from https://manual.calibre-ebook.com/sub_groups.html or can be "series"
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(series) && !string.IsNullOrEmpty(seriesIndex))
                {
                    if (string.IsNullOrEmpty(specialName))
                    {
                        specialName = epubBook.Title;
                    }
                    var info = new ParserInfo
                    {
                        Chapters = Parser.DefaultChapter,
                        Edition = string.Empty,
                        Format = MangaFormat.Epub,
                        Filename = Path.GetFileName(filePath),
                        Title = specialName?.Trim() ?? string.Empty,
                        FullFilePath = Parser.NormalizePath(filePath),
                        IsSpecial = Parser.HasSpecialMarker(filePath),
                        Series = series.Trim(),
                        SeriesSort = series.Trim(),
                        Volumes = seriesIndex
                    };

                    return info;
                }
            }
            catch (Exception)
            {
                // Swallow exception
            }

            return new ParserInfo
            {
                Chapters = Parser.DefaultChapter,
                Edition = string.Empty,
                Format = MangaFormat.Epub,
                Filename = Path.GetFileName(filePath),
                Title = epubBook.Title.Trim(),
                FullFilePath = Parser.NormalizePath(filePath),
                IsSpecial = Parser.HasSpecialMarker(filePath),
                Series = epubBook.Title.Trim(),
                Volumes = Parser.LooseLeafVolume,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BookService] There was an exception when opening epub book: {FileName}", filePath);
            _mediaErrorService.ReportMediaIssue(filePath, MediaErrorProducer.BookService,
                "There was an exception when opening epub book", ex);
        }

        return null;
    }

    /// <summary>
    /// Extracts a pdf into images to a target directory. Uses multithreaded implementation since docnet is slow normally.
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
    private async Task<string> ScopePage(HtmlDocument doc, EpubBookRef book, string apiBase, HtmlNode body, Dictionary<string, int> mappings, int page)
    {
        await InlineStyles(doc, book, apiBase, body);

        RewriteAnchors(page, doc, mappings);

        ScopeImages(doc, book, apiBase);

        return PrepareFinalHtml(doc, body);
    }

    /// <summary>
    /// Tries to find the correct key by applying cleaning and remapping if the epub has bad data. Only works for HTML files.
    /// </summary>
    /// <param name="book"></param>
    /// <param name="mappings"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    private static string? CoalesceKey(EpubBookRef book, IReadOnlyDictionary<string, int> mappings, string? key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        if (mappings.ContainsKey(CleanContentKeys(key))) return key;

        // Fallback to searching for key (bad epub metadata)
        var correctedKey = book.Content.Html.Local.Select(s => s.Key).FirstOrDefault(s => s.EndsWith(key));
        if (!string.IsNullOrEmpty(correctedKey))
        {
            key = correctedKey;
        }

        var stepsBack = CountParentDirectory(book.Content.NavigationHtmlFile?.FilePath);
        if (mappings.TryGetValue(key, out _))
        {
            return key;
        }

        var modifiedKey = RemovePathSegments(key, stepsBack);
        if (mappings.TryGetValue(modifiedKey, out _))
        {
            return modifiedKey;
        }


        return key;
    }

    public static string CoalesceKeyForAnyFile(EpubBookRef book, string key)
    {
        if (book.Content.AllFiles.ContainsLocalFileRefWithKey(key)) return key;

        var cleanedKey = CleanContentKeys(key);
        if (book.Content.AllFiles.ContainsLocalFileRefWithKey(cleanedKey)) return cleanedKey;

        // TODO: Figure this out
        // Fallback to searching for key (bad epub metadata)
        // var correctedKey = book.Content.AllFiles.Keys.SingleOrDefault(s => s.EndsWith(key));
        // if (!string.IsNullOrEmpty(correctedKey))
        // {
        //     key = correctedKey;
        // }

        return key;
    }

    /// <summary>
    /// This will return a list of mappings from ID -> page num. ID will be the xhtml key and page num will be the reading order
    /// this is used to rewrite anchors in the book text so that we always load properly in our reader.
    /// </summary>
    /// <param name="chapter">Chapter with at least one file</param>
    /// <returns></returns>
    public async Task<ICollection<BookChapterItem>> GenerateTableOfContents(Chapter chapter)
    {
        using var book = await EpubReader.OpenBookAsync(chapter.Files.ElementAt(0).FilePath, BookReaderOptions);
        var mappings = await CreateKeyToPageMappingAsync(book);

        var navItems = await book.GetNavigationAsync();
        var chaptersList = new List<BookChapterItem>();

        if (navItems != null)
        {
            foreach (var navigationItem in navItems)
            {
                if (navigationItem.NestedItems.Count == 0)
                {
                    CreateToCChapter(book, navigationItem, Array.Empty<BookChapterItem>(), chaptersList, mappings);
                    continue;
                }

                var nestedChapters = new List<BookChapterItem>();

                foreach (var nestedChapter in navigationItem.NestedItems.Where(n => n.Link != null))
                {
                    var key = CoalesceKey(book, mappings, nestedChapter.Link?.ContentFilePath);
                    if (mappings.TryGetValue(key, out var mapping))
                    {
                        nestedChapters.Add(new BookChapterItem
                        {
                            Title = nestedChapter.Title,
                            Page = mapping,
                            Part = nestedChapter.Link?.Anchor ?? string.Empty,
                            Children = new List<BookChapterItem>()
                        });
                    }
                }

                CreateToCChapter(book, navigationItem, nestedChapters, chaptersList, mappings);
            }
        }

        if (chaptersList.Count != 0) return chaptersList;
        // Generate from TOC from links (any point past this, Kavita is generating as a TOC doesn't exist)
        var tocPage = book.Content.Html.Local.Select(s => s.Key)
            .FirstOrDefault(k => k.Equals("TOC.XHTML", StringComparison.InvariantCultureIgnoreCase) ||
            k.Equals("NAVIGATION.XHTML", StringComparison.InvariantCultureIgnoreCase));
        if (string.IsNullOrEmpty(tocPage)) return chaptersList;


        // Find all anchor tags, for each anchor we get inner text, to lower then title case on UI. Get href and generate page content
        if (!book.Content.Html.TryGetLocalFileRefByKey(tocPage, out var file)) return chaptersList;
        var content = await file.ReadContentAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        // TODO: We may want to check if there is a toc.ncs file to better handle nested toc
        // We could do a fallback first with ol/lis



        var anchors = doc.DocumentNode.SelectNodes("//a");
        if (anchors == null) return chaptersList;

        foreach (var anchor in anchors)
        {
            if (!anchor.Attributes.Contains("href")) continue;

            var key = CoalesceKey(book, mappings, anchor.Attributes["href"].Value.Split("#")[0]);

            if (string.IsNullOrEmpty(key) || !mappings.ContainsKey(key)) continue;
            var part = string.Empty;
            if (anchor.Attributes["href"].Value.Contains('#'))
            {
                part = anchor.Attributes["href"].Value.Split("#")[1];
            }
            chaptersList.Add(new BookChapterItem
            {
                Title = anchor.InnerText,
                Page = mappings[key],
                Part = part,
                Children = new List<BookChapterItem>()
            });
        }

        return chaptersList;
    }

    private static int CountParentDirectory(string path)
    {
        const string pattern = @"\.\./";
        var matches = Regex.Matches(path, pattern);

        return matches.Count;
    }

    /// <summary>
    /// Removes paths segments from the beginning of a path. Returns original path if any issues.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="segmentsToRemove"></param>
    /// <returns></returns>
    private static string RemovePathSegments(string path, int segmentsToRemove)
    {
        if (segmentsToRemove <= 0)
            return path;

        var startIndex = 0;
        for (var i = 0; i < segmentsToRemove; i++)
        {
            var slashIndex = path.IndexOf('/', startIndex);
            if (slashIndex == -1)
                return path; // Not enough segments to remove

            startIndex = slashIndex + 1;
        }

        return path.Substring(startIndex);
    }

    /// <summary>
    /// This returns a single page within the epub book. All html will be rewritten to be scoped within our reader,
    /// all css is scoped, etc.
    /// </summary>
    /// <param name="page">The requested page</param>
    /// <param name="chapterId">The chapterId</param>
    /// <param name="cachedEpubPath">The path to the cached epub file</param>
    /// <param name="baseUrl">The API base for Kavita, to rewrite urls to so we load though our endpoint</param>
    /// <returns>Full epub HTML Page, scoped to Kavita's reader</returns>
    /// <exception cref="KavitaException">All exceptions throw this</exception>
    public async Task<string> GetBookPage(int page, int chapterId, string cachedEpubPath, string baseUrl)
    {
        using var book = await EpubReader.OpenBookAsync(cachedEpubPath, BookReaderOptions);
        var mappings = await CreateKeyToPageMappingAsync(book);
        var apiBase = baseUrl + "book/" + chapterId + "/" + BookApiUrl;

        var counter = 0;
        var doc = new HtmlDocument {OptionFixNestedTags = true};


        var bookPages = await book.GetReadingOrderAsync();
        try
        {
            foreach (var contentFileRef in bookPages)
            {
                if (page != counter)
                {
                    counter++;
                    continue;
                }

                var content = await contentFileRef.ReadContentAsync();
                if (contentFileRef.ContentType != EpubContentType.XHTML_1_1) return content;

                // In more cases than not, due to this being XML not HTML, we need to escape the script tags.
                content = EscapeTags(content);

                doc.LoadHtml(content);
                var body = doc.DocumentNode.SelectSingleNode("//body");

                if (body == null)
                {
                    if (doc.ParseErrors.Any())
                    {
                        LogBookErrors(book, contentFileRef, doc);
                        throw new KavitaException("epub-malformed");
                    }
                    _logger.LogError("{FilePath} has no body tag! Generating one for support. Book may be skewed", book.FilePath);
                    doc.DocumentNode.SelectSingleNode("/html").AppendChild(HtmlNode.CreateNode("<body></body>"));
                    body = doc.DocumentNode.SelectSingleNode("/html/body");
                }

                return await ScopePage(doc, book, apiBase, body, mappings, page);
            }
        } catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue reading one of the pages for {Book}", book.FilePath);
            await _mediaErrorService.ReportMediaIssueAsync(book.FilePath, MediaErrorProducer.BookService,
                "There was an issue reading one of the pages for", ex);
        }

        throw new KavitaException("epub-html-missing");
    }

    private static void CreateToCChapter(EpubBookRef book, EpubNavigationItemRef navigationItem, IList<BookChapterItem> nestedChapters,
        ICollection<BookChapterItem> chaptersList, IReadOnlyDictionary<string, int> mappings)
    {
        if (navigationItem.Link == null)
        {
            var item = new BookChapterItem
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
            var groupKey = CoalesceKey(book, mappings, navigationItem.Link.ContentFilePath);
            if (mappings.ContainsKey(groupKey))
            {
                chaptersList.Add(new BookChapterItem
                {
                    Title = navigationItem.Title,
                    Page = mappings[groupKey],
                    Children = nestedChapters
                });
            }
        }
    }


    /// <summary>
    /// Extracts the cover image to covers directory and returns file path back
    /// </summary>
    /// <param name="fileFilePath"></param>
    /// <param name="fileName">Name of the new file.</param>
    /// <param name="outputDirectory">Where to output the file, defaults to covers directory</param>
    /// <param name="encodeFormat">When saving the file, use encoding</param>
    /// <returns></returns>
    public string GetCoverImage(string fileFilePath, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        if (!IsValidFile(fileFilePath)) return string.Empty;

        if (Parser.IsPdf(fileFilePath))
        {
            return GetPdfCoverImage(fileFilePath, fileName, outputDirectory, encodeFormat, size);
        }

        using var epubBook = EpubReader.OpenBook(fileFilePath, BookReaderOptions);

        try
        {
            // Try to get the cover image from OPF file, if not set, try to parse it from all the files, then result to the first one.
            var coverImageContent = epubBook.Content.Cover
                                    ?? epubBook.Content.Images.Local.FirstOrDefault(file => Parser.IsCoverImage(file.FilePath))
                                    ?? epubBook.Content.Images.Local.FirstOrDefault();

            if (coverImageContent == null) return string.Empty;
            using var stream = coverImageContent.GetContentStream();

            return _imageService.WriteCoverThumbnail(stream, fileName, outputDirectory, encodeFormat, size);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BookService] There was a critical error and prevented thumbnail generation on {BookFile}. Defaulting to no cover image", fileFilePath);
            _mediaErrorService.ReportMediaIssue(fileFilePath, MediaErrorProducer.BookService,
                "There was a critical error and prevented thumbnail generation", ex);
        }

        return string.Empty;
    }


    private string GetPdfCoverImage(string fileFilePath, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size)
    {
        try
        {
            using var docReader = DocLib.Instance.GetDocReader(fileFilePath, new PageDimensions(1080, 1920));
            if (docReader.GetPageCount() == 0) return string.Empty;

            using var stream = StreamManager.GetStream("BookService.GetPdfPage");
            GetPdfPage(docReader, 0, stream);

            return _imageService.WriteCoverThumbnail(stream, fileName, outputDirectory, encodeFormat, size);

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[BookService] There was a critical error and prevented thumbnail generation on {BookFile}. Defaulting to no cover image",
                fileFilePath);
            _mediaErrorService.ReportMediaIssue(fileFilePath, MediaErrorProducer.BookService,
                "There was a critical error and prevented thumbnail generation", ex);
        }

        return string.Empty;
    }

    /// <summary>
    /// Returns an image raster of a page within a PDF
    /// </summary>
    /// <param name="docReader"></param>
    /// <param name="pageNumber"></param>
    /// <param name="stream"></param>
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
        body = Regex.Replace(body, @"/\*[\d\D]*?\*/", string.Empty, RegexOptions.None, Parser.RegexTimeout);

        body = Regex.Replace(body, @"[a-zA-Z]+#", "#", RegexOptions.None, Parser.RegexTimeout);
        body = Regex.Replace(body, @"[\n\r]+\s*", string.Empty, RegexOptions.None, Parser.RegexTimeout);
        body = Regex.Replace(body, @"\s+", " ", RegexOptions.None, Parser.RegexTimeout);
        body = Regex.Replace(body, @"\s?([:,;{}])\s?", "$1", RegexOptions.None, Parser.RegexTimeout);

        // Handle <!-- which some books use (but shouldn't)
        body = Regex.Replace(body, "<!--.*?-->", string.Empty, RegexOptions.None, Parser.RegexTimeout);

        // Handle /* */
        body = Regex.Replace(body, @"/\*.*?\*/", string.Empty, RegexOptions.None, Parser.RegexTimeout);

        try
        {
            body = body.Replace(";}", "}");
        }
        catch (Exception)
        {
            //Swallow exception. Some css don't have style rules ending in ';'
        }

        body = Regex.Replace(body, @"([\s:]0)(px|pt|%|em)", "$1", RegexOptions.None, Parser.RegexTimeout);


        return body;
    }

    private void LogBookErrors(EpubBookRef book, EpubContentFileRef contentFileRef, HtmlDocument doc)
    {
        _logger.LogError("{FilePath} has an invalid html file (Page {PageName})", book.FilePath, contentFileRef.Key);
        foreach (var error in doc.ParseErrors)
        {
            _logger.LogError("Line {LineNumber}, Reason: {Reason}", error.Line, error.Reason);
        }
    }
}
