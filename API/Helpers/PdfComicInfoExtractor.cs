using System;
using System.Xml;
using System.Text;
using System.IO;
using System.Diagnostics;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using Nager.ArticleNumber;
using System.Collections.Generic;

namespace API.Helpers;
#nullable enable

public interface IPdfComicInfoExtractor
{
    ComicInfo? GetComicInfo(string filePath);
}

public class PdfComicInfoExtractor : IPdfComicInfoExtractor
{
    private readonly ILogger<BookService> _logger;
    private readonly IMediaErrorService _mediaErrorService;

    public PdfComicInfoExtractor(ILogger<BookService> logger, IMediaErrorService mediaErrorService)
    {
        _logger = logger;
        _mediaErrorService = mediaErrorService;
    }

    private string? GetTextFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        return (doc.DocumentElement?.SelectSingleNode(path + "//rdf:li", ns)
            ?? doc.DocumentElement?.SelectSingleNode(path, ns))?.InnerText;
    }

    private float? GetFloatFromText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        return float.Parse(text);
    }

    private DateTime? GetDateTimeFromText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        DateTime date;

        if (DateTime.TryParse(text, out date))
        {
            return date;
        }

        // Normalize possible PDF date
        if (text[0] != 'D') {
            text = "D:" + text;
        }
        text = text.Replace("'", ":");
        text = text.Replace("Z", "+");

        string[] pdfDateFormats = [
            "D:yyyyMMddHHmmsszzz:", "D:yyyyMMddHHmmss+", "D:yyyyMMddHHmmss",
            "D:yyyyMMddHHmmzzz:",  "D:yyyyMMddHHmm+",   "D:yyyyMMddHHmm",
            "D:yyyyMMddHHzzz:", "D:yyyyMMddHH+", "D:yyyyMMddHH",
            "D:yyyyMMdd", "D:yyyyMM", "D:yyyy"
        ];

        foreach(var format in pdfDateFormats)
        {
            if (DateTime.TryParseExact(text, format, null, System.Globalization.DateTimeStyles.None, out date))
            {
                return date;
            }
        }

        return null;
    }

    private string? MaybeGetMetadata(Dictionary<String, String> metadata, string key)
    {
        return metadata.ContainsKey(key) ? metadata[key] : null;
    }

    private ComicInfo? GetComicInfoFromMetadata(Dictionary<String, String> metadata, string filePath)
    {
        var info = new ComicInfo();

        var publicationDate = GetDateTimeFromText(MaybeGetMetadata(metadata, "CreationDate"));

        if (publicationDate != null)
        {
            info.Year  = publicationDate.Value.Year;
            info.Month = publicationDate.Value.Month;
            info.Day   = publicationDate.Value.Day;
        }

        info.Summary   = MaybeGetMetadata(metadata, "Summary") ?? String.Empty;
        info.Publisher = MaybeGetMetadata(metadata, "Publisher") ?? String.Empty;
        info.Writer    = MaybeGetMetadata(metadata, "Author") ?? String.Empty;
        info.Title     = MaybeGetMetadata(metadata, "Title") ?? String.Empty;
        info.Genre     = MaybeGetMetadata(metadata, "Subject") ?? String.Empty;
        info.LanguageISO = BookService.ValidateLanguage(MaybeGetMetadata(metadata, "Language"));
        info.Isbn      = MaybeGetMetadata(metadata, "ISBN") ?? String.Empty;

        if (info.Isbn != String.Empty && !ArticleNumberHelper.IsValidIsbn10(info.Isbn) && !ArticleNumberHelper.IsValidIsbn13(info.Isbn))
        {
            _logger.LogDebug("[BookService] {File} has an invalid ISBN number", filePath);
            info.Isbn = String.Empty;
        }

        info.UserRating = GetFloatFromText(MaybeGetMetadata(metadata, "UserRating")) ?? 0.0f;
        info.TitleSort  = MaybeGetMetadata(metadata, "TitleSort") ?? String.Empty;
        info.Series     = MaybeGetMetadata(metadata, "Series") ?? String.Empty;
        info.SeriesSort = info.Series;
        info.Volume     = Convert.ToInt32(GetFloatFromText(MaybeGetMetadata(metadata, "Volume")) ?? 0.0f).ToString();

        // If this is a single book and not a collection, set publication status to Completed
        if (string.IsNullOrEmpty(info.Volume) && Parser.ParseVolume(filePath, LibraryType.Manga).Equals(Parser.LooseLeafVolume))
        {
            info.Count = 1;
        }

        var hasVolumeInSeries = !Parser.ParseVolume(info.Title, LibraryType.Manga)
            .Equals(Parser.LooseLeafVolume);

        if (string.IsNullOrEmpty(info.Volume) && hasVolumeInSeries && (!info.Series.Equals(info.Title) || string.IsNullOrEmpty(info.Series)))
        {
            // This is likely a light novel for which we can set series from parsed title
            info.Series = Parser.ParseSeries(info.Title, LibraryType.Manga);
            info.Volume = Parser.ParseVolume(info.Title, LibraryType.Manga);
        }

        ComicInfo.CleanComicInfo(info);

        return info;
    }

    public ComicInfo? GetComicInfo(string filePath)
    {
        try
        {
            var extractor = new PdfMetadataExtractor(_logger, filePath);

            return GetComicInfoFromMetadata(extractor.GetMetadata(), filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetComicInfo] There was an exception parsing PDF metadata for {File}", filePath);
            _mediaErrorService.ReportMediaIssue(filePath, MediaErrorProducer.BookService,
                "There was an exception parsing PDF metadata", ex);
        }

        return null;
    }
}