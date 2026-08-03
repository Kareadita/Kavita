using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Kavita.Services.Kobo;

/// <summary>
/// Reads convert EPUB/KEPUB spine length for page-count trust checks.
/// </summary>
public static class KoboConvertEpubInspector
{
    private static readonly XNamespace OpfNs = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace ContainerNs = "urn:oasis:names:tc:opendocument:xmlns:container";

    /// <summary>
    /// Counts spine <c>itemref</c> entries in the package (content docs only under the convert contract).
    /// Returns null when the package cannot be read.
    /// </summary>
    public static int? TryCountSpinePages(string epubPath)
    {
        if (string.IsNullOrWhiteSpace(epubPath) || !File.Exists(epubPath)) return null;

        try
        {
            using var zip = ZipFile.OpenRead(epubPath);
            var opfPath = ResolveOpfPath(zip);
            if (opfPath == null) return null;

            var opfEntry = zip.GetEntry(opfPath);
            if (opfEntry == null) return null;

            using var stream = opfEntry.Open();
            var opf = XDocument.Load(stream);
            var spine = opf.Root?.Element(OpfNs + "spine");
            if (spine == null) return null;

            return spine.Elements(OpfNs + "itemref").Count();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Test/helper seam: writes a minimal convert-shaped EPUB with <paramref name="pageCount"/> spine docs.
    /// </summary>
    public static void WriteMinimalConvertEpub(string outputPath, int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
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
            var id = $"page_{i:D4}";
            manifestItems.Add(new XElement(OpfNs + "item",
                new XAttribute("id", id),
                new XAttribute("href", $"Text/{id}.xhtml"),
                new XAttribute("media-type", "application/xhtml+xml")));
            spineItems.Add(new XElement(OpfNs + "itemref", new XAttribute("idref", id)));
        }

        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        var opf = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(OpfNs + "package",
                new XAttribute("version", "3.0"),
                new XAttribute("unique-identifier", "bookid"),
                new XElement(OpfNs + "metadata",
                    new XElement(dc + "identifier", new XAttribute("id", "bookid"), "test"),
                    new XElement(dc + "title", "test"),
                    new XElement(dc + "language", "en")),
                new XElement(OpfNs + "manifest", manifestItems),
                new XElement(OpfNs + "spine", spineItems)));

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mime.Open(), new UTF8Encoding(false)))
        {
            writer.Write("application/epub+zip");
        }

        var container = zip.CreateEntry("META-INF/container.xml");
        using (var writer = new StreamWriter(container.Open(), Encoding.UTF8))
        {
            writer.Write(
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """);
        }

        var opfEntry = zip.CreateEntry("OEBPS/content.opf");
        using (var stream = opfEntry.Open())
        {
            opf.Save(stream);
        }

        for (var i = 1; i <= pageCount; i++)
        {
            var page = zip.CreateEntry($"OEBPS/Text/page_{i:D4}.xhtml");
            using var writer = new StreamWriter(page.Open(), Encoding.UTF8);
            writer.Write($"<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><img alt=\"{i}\"/></body></html>");
        }
    }

    private static string? ResolveOpfPath(ZipArchive zip)
    {
        var container = zip.GetEntry("META-INF/container.xml");
        if (container != null)
        {
            try
            {
                using var stream = container.Open();
                var doc = XDocument.Load(stream);
                var fullPath = doc.Root?
                    .Element(ContainerNs + "rootfiles")?
                    .Element(ContainerNs + "rootfile")?
                    .Attribute("full-path")?
                    .Value;
                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    return fullPath.Replace('\\', '/');
                }
            }
            catch
            {
                // Fall through to default convert path.
            }
        }

        return zip.GetEntry("OEBPS/content.opf") != null ? "OEBPS/content.opf" : null;
    }
}
