using System;
using System.Xml;
using System.Text;
using System.IO;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using Nager.ArticleNumber;

namespace API.Helpers;
#nullable enable

public interface IPdfMetadataExtractor 
{
    ComicInfo? GetComicInfo(string filePath);
}

public class PdfMetadataExtractor : IPdfMetadataExtractor
{
    private readonly ILogger<BookService> _logger;
    private readonly IMediaErrorService _mediaErrorService;

    public PdfMetadataExtractor(ILogger<BookService> logger, IMediaErrorService mediaErrorService)
    {
        _logger = logger;
        _mediaErrorService = mediaErrorService;
    }

    private int FindInBuffer(byte[] buffer, int bufLen, byte[] match)
    {
        var maxPos = bufLen - match.Length;
        for (var pos = 0; pos<=maxPos; ++pos)
        {
            var found = true;
            for (var ch = 0; ch<match.Length; ++ch)
            {
                if (buffer[pos+ch] != match[ch])
                {
                    found = false;
                    break;
                }
            }
            if (found)
            {
                return pos;
            }
        }
        return -1;
    }

    private string? GetTextFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        return (doc.DocumentElement?.SelectSingleNode(path + "//rdf:li", ns)
            ?? doc.DocumentElement?.SelectSingleNode(path, ns))?.InnerText;
    }

    private float? GetFloatFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        var text = GetTextFromXmlNode(doc, ns, path);
        if (string.IsNullOrEmpty(text)) return null;

        return float.Parse(text);
    }

    private string? GetListFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        var nodes = doc.DocumentElement?.SelectNodes(path+"//rdf:li", ns);
        if (nodes == null) return null;
        var list = new StringBuilder();
        foreach (XmlNode n in nodes)
        {
            if (list.Length > 0)
            {
                list.Append(",");
            }
            list.Append(n.InnerText);
        }
        return list.Length > 0 ? list.ToString() : null;
    }

    private DateTime? GetDateTimeFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        var text = GetTextFromXmlNode(doc, ns, path);
        if (text == null) return null;

        return DateTime.Parse(text);
    }

    private ComicInfo? GetComicInfoFromMetadata(string metadata, string filePath)
    {
        var metaDoc = new XmlDocument();
        metaDoc.LoadXml(metadata);

        var ns = new XmlNamespaceManager(metaDoc.NameTable);
        ns.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
        ns.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
        ns.AddNamespace("calibreSI", "http://calibre-ebook.com/xmp-namespace-series-index");
        ns.AddNamespace("calibre", "http://calibre-ebook.com/xmp-namespace");
        ns.AddNamespace("pdfx", "http://ns.adobe.com/pdfx/1.3/");
        ns.AddNamespace("prism", "http://prismstandard.org/namespaces/basic/2.0/");
        ns.AddNamespace("xmp", "http://ns.adobe.com/xap/1.0/");

        var info = new ComicInfo();
        var publicationDate = GetDateTimeFromXmlNode(metaDoc, ns, "//dc:date")
            ?? GetDateTimeFromXmlNode(metaDoc, ns, "//xmp:createdate");
        if (publicationDate != null)
        {
            info.Year  = publicationDate.Value.Year;
            info.Month = publicationDate.Value.Month;
            info.Day   = publicationDate.Value.Day;
        }

        info.Summary   = GetTextFromXmlNode(metaDoc, ns, "//dc:description") ?? String.Empty;
        info.Publisher = GetTextFromXmlNode(metaDoc, ns, "//dc:publisher") ?? String.Empty;
        info.Writer    = GetListFromXmlNode(metaDoc, ns, "//dc:creator") ?? String.Empty;
        info.Title     = GetTextFromXmlNode(metaDoc, ns, "//dc:title") ?? String.Empty;
        info.Genre     = GetListFromXmlNode(metaDoc, ns, "//dc:subject") ?? String.Empty;
        info.LanguageISO = BookService.ValidateLanguage(GetTextFromXmlNode(metaDoc, ns, "//dc:language"));

        info.Isbn = GetTextFromXmlNode(metaDoc, ns, "//pdfx:isbn") ?? GetTextFromXmlNode(metaDoc, ns, "//prism:isbn") ?? String.Empty;
        if (!ArticleNumberHelper.IsValidIsbn10(info.Isbn) && !ArticleNumberHelper.IsValidIsbn13(info.Isbn))
        {
            _logger.LogDebug("[BookService] {File} has invalid ISBN number", filePath);
            info.Isbn = String.Empty;
        }

        info.UserRating = GetFloatFromXmlNode(metaDoc, ns, "//calibre:rating") ?? 0.0f;
        info.TitleSort  = GetTextFromXmlNode(metaDoc, ns, "//calibre:title_sort") ?? String.Empty;
        info.Series     = GetTextFromXmlNode(metaDoc, ns, "//calibre:series/rdf:value") ?? String.Empty;
        info.SeriesSort = info.Series;
        info.Volume     = Convert.ToInt32(GetFloatFromXmlNode(metaDoc, ns, "//calibreSI:series_index") ?? 0.0f).ToString();

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
            const int chunkSize = 4096;
            const int overlap = 16;
            var stream = File.OpenRead(filePath);
            var buffer = new byte[chunkSize + overlap];
            var bytesAvailable = 0;
            var hasMetaData = false;
            var meta = new byte[0];
            while (!hasMetaData)
            {
                var bytesRead = stream.Read(buffer, bytesAvailable, chunkSize);
                if (bytesRead == 0) break;
                bytesAvailable += bytesRead;
                var found = FindInBuffer(buffer, bytesAvailable, Encoding.UTF8.GetBytes("<x:xmpmeta"));
                if (found >= 0)
                {
                    meta = buffer[found..bytesAvailable];
                    hasMetaData = true;
                    break;
                }
                else
                {
                    var ovl = Math.Min(overlap, bytesAvailable);
                    Buffer.BlockCopy(buffer, bytesAvailable - ovl, buffer, 0, ovl);
                    bytesAvailable = ovl;
                }
            }
            while ((bytesAvailable = stream.Read(buffer, 0, chunkSize)) > 0 || hasMetaData)
            {
                hasMetaData = false;
                if (bytesAvailable > 0) {
                    byte[] newMeta = new byte[meta.Length + bytesAvailable];
                    Buffer.BlockCopy(meta, 0, newMeta, 0, meta.Length);
                    Buffer.BlockCopy(buffer, 0, newMeta, meta.Length, bytesAvailable);
                    meta = newMeta;
                }
                var found = FindInBuffer(meta, meta.Length, Encoding.UTF8.GetBytes("</x:xmpmeta>"));
                if (found >= 0)
                {
                    var metadata = meta[0..(found + "</x:xmpmeta>".Length)];
                    return GetComicInfoFromMetadata(Encoding.UTF8.GetString(metadata), filePath);
                }
            }
            return null; // Unterminated metadata
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetComicInfo] There was an exception parsing PDF metadata");
            _mediaErrorService.ReportMediaIssue(filePath, MediaErrorProducer.BookService,
                "There was an exception parsing PDF metadata", ex);
        }

        return null;
    }
}