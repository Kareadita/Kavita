using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Kavita.Services.Kobo;

namespace Kavita.Services.Tests.Kobo;

/// <summary>
/// Test helper that writes a minimal convert-shaped EPUB (matching <see cref="KoboConvertEpubContract"/>)
/// with a fixed spine length, for exercising page-count trust checks.
/// </summary>
public static class KoboConvertEpubTestFactory
{
    private static readonly XNamespace OpfNs = KoboConvertEpubContract.OpfNs;
    private static readonly XNamespace DcNs = KoboConvertEpubContract.DcNs;

    public static void WriteMinimalConvertEpub(string outputPath, int pageCount)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (File.Exists(outputPath)) File.Delete(outputPath);

        var manifestItems = new List<XElement>
        {
            new(OpfNs + "item",
                new XAttribute("id", "nav"),
                new XAttribute("href", "nav.xhtml"),
                new XAttribute("media-type", "application/xhtml+xml"),
                new XAttribute("properties", "nav")),
        };
        var spineItems = new List<XElement>();
        for (var i = 1; i <= pageCount; i++)
        {
            var id = KoboConvertEpubContract.PageId(i);
            manifestItems.Add(new XElement(OpfNs + "item",
                new XAttribute("id", id),
                new XAttribute("href", $"{KoboConvertEpubContract.TextFolder}/{KoboConvertEpubContract.PageFileName(i)}"),
                new XAttribute("media-type", "application/xhtml+xml")));
            spineItems.Add(new XElement(OpfNs + "itemref", new XAttribute("idref", id)));
        }

        var opf = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(OpfNs + "package",
                new XAttribute("version", "3.0"),
                new XAttribute("unique-identifier", "bookid"),
                new XElement(OpfNs + "metadata",
                    new XElement(DcNs + "identifier", new XAttribute("id", "bookid"), "test"),
                    new XElement(DcNs + "title", "test"),
                    new XElement(DcNs + "language", "en")),
                new XElement(OpfNs + "manifest", manifestItems),
                new XElement(OpfNs + "spine", spineItems)));

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        var mime = zip.CreateEntry(KoboConvertEpubContract.MimeTypeEntry, CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mime.Open(), new UTF8Encoding(false)))
        {
            writer.Write(KoboConvertEpubContract.MimeType);
        }

        var container = zip.CreateEntry(KoboConvertEpubContract.ContainerPath);
        using (var writer = new StreamWriter(container.Open(), Encoding.UTF8))
        {
            writer.Write(KoboConvertEpubContract.ContainerXml);
        }

        var opfEntry = zip.CreateEntry(KoboConvertEpubContract.OpfPath);
        using (var stream = opfEntry.Open())
        {
            opf.Save(stream);
        }

        for (var i = 1; i <= pageCount; i++)
        {
            var page = zip.CreateEntry(
                $"{KoboConvertEpubContract.OebpsFolder}/{KoboConvertEpubContract.TextFolder}/{KoboConvertEpubContract.PageFileName(i)}");
            using var writer = new StreamWriter(page.Open(), Encoding.UTF8);
            writer.Write($"<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><img alt=\"{i}\"/></body></html>");
        }
    }
}
