using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Builds a minimal reflowable EPUB3 from comic archive images (CBZ/CBR).
/// Frozen structural contract (bump <see cref="KoboConversionService.ConvertContractVersion"/> on change):
/// one spine doc per image at <c>OEBPS/Text/page_NNNN.xhtml</c> with a single <c>img</c>;
/// matching <c>OEBPS/Images/page_NNNN{ext}</c>; ids <c>page_NNNN</c>/<c>img_NNNN</c>;
/// nav in manifest only (never spine); no cover meta; no hand-authored Kobo spans.
/// </summary>
public class KoboArchiveEpubConverter(
    ILogger<KoboArchiveEpubConverter> logger,
    IArchiveService archiveService,
    IDirectoryService directoryService)
    : IKoboArchiveEpubConverter
{
    private static readonly XNamespace OpfNs = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace XhtmlNs = "http://www.w3.org/1999/xhtml";

    public Task ConvertAsync(string archivePath, string outputEpubPath, string title,
        CancellationToken ct = default)
    {
        return Task.Run(() => ConvertSync(archivePath, outputEpubPath, title, ct), ct);
    }

    private void ConvertSync(string archivePath, string outputEpubPath, string title, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            throw new FileNotFoundException("Kobo convert source archive missing", archivePath);
        }

        var workRoot = Path.Combine(directoryService.TempDirectory, "kobo-convert",
            Guid.NewGuid().ToString("N"));
        var extractDir = Path.Combine(workRoot, "extract");
        var stagingDir = Path.Combine(workRoot, "epub");
        // ExtractArchive no-ops when extractPath already exists — do not pre-create it.
        directoryService.ExistOrCreate(workRoot);
        directoryService.ExistOrCreate(stagingDir);

        try
        {
            ct.ThrowIfCancellationRequested();
            archiveService.ExtractArchive(archivePath, extractDir);

            var images = Directory.EnumerateFiles(extractDir, "*", SearchOption.AllDirectories)
                .Where(p => !Parser.HasBlacklistedFolderInPath(Path.GetDirectoryName(p) ?? string.Empty))
                .Where(Parser.IsImage)
                .OrderByNatural(p => p.GetFullPathWithoutExtension())
                .ToList();

            if (images.Count == 0)
            {
                throw new InvalidOperationException($"No images found in archive: {archivePath}");
            }

            var metaInf = Path.Combine(stagingDir, "META-INF");
            var oebps = Path.Combine(stagingDir, "OEBPS");
            var imagesDir = Path.Combine(oebps, "Images");
            var textDir = Path.Combine(oebps, "Text");
            directoryService.ExistOrCreate(metaInf);
            directoryService.ExistOrCreate(imagesDir);
            directoryService.ExistOrCreate(textDir);

            File.WriteAllText(Path.Combine(stagingDir, "mimetype"), "application/epub+zip",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(Path.Combine(metaInf, "container.xml"),
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """);

            var manifestItems = new List<XElement>();
            var spineItems = new List<XElement>();
            var navLis = new List<XElement>();
            var bookTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(archivePath) : title;

            for (var i = 0; i < images.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var source = images[i];
                var ext = Path.GetExtension(source).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                var imageFileName = $"page_{i + 1:D4}{ext}";
                var pageFileName = $"page_{i + 1:D4}.xhtml";
                var imageId = $"img_{i + 1:D4}";
                var pageId = $"page_{i + 1:D4}";

                File.Copy(source, Path.Combine(imagesDir, imageFileName), overwrite: true);
                var pageHtml =
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    "<!DOCTYPE html>\n" +
                    "<html xmlns=\"http://www.w3.org/1999/xhtml\">\n" +
                    "<head>\n" +
                    $"<title>{EscapeXml(bookTitle)} — {i + 1}</title>\n" +
                    "<meta charset=\"utf-8\"/>\n" +
                    "<style type=\"text/css\">html, body { margin: 0; padding: 0; text-align: center; background: #000; } img { max-width: 100%; height: auto; }</style>\n" +
                    "</head>\n" +
                    "<body>\n" +
                    $"<img src=\"../Images/{imageFileName}\" alt=\"Page {i + 1}\"/>\n" +
                    "</body>\n" +
                    "</html>\n";
                File.WriteAllText(Path.Combine(textDir, pageFileName), pageHtml, Encoding.UTF8);

                manifestItems.Add(new XElement(OpfNs + "item",
                    new XAttribute("id", imageId),
                    new XAttribute("href", $"Images/{imageFileName}"),
                    new XAttribute("media-type", MediaTypeForExtension(ext))));
                manifestItems.Add(new XElement(OpfNs + "item",
                    new XAttribute("id", pageId),
                    new XAttribute("href", $"Text/{pageFileName}"),
                    new XAttribute("media-type", "application/xhtml+xml")));
                spineItems.Add(new XElement(OpfNs + "itemref", new XAttribute("idref", pageId)));
                navLis.Add(new XElement(XhtmlNs + "li",
                    new XElement(XhtmlNs + "a",
                        new XAttribute("href", $"Text/{pageFileName}"),
                        $"Page {i + 1}")));
            }

            manifestItems.Insert(0, new XElement(OpfNs + "item",
                new XAttribute("id", "nav"),
                new XAttribute("href", "nav.xhtml"),
                new XAttribute("media-type", "application/xhtml+xml"),
                new XAttribute("properties", "nav")));

            var opf = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(OpfNs + "package",
                    new XAttribute("version", "3.0"),
                    new XAttribute("unique-identifier", "bookid"),
                    new XAttribute(XNamespace.Xml + "lang", "en"),
                    new XElement(OpfNs + "metadata",
                        new XAttribute(XNamespace.Xmlns + "dc", DcNs),
                        new XElement(DcNs + "identifier",
                            new XAttribute("id", "bookid"),
                            $"kavita-kobo-{Guid.NewGuid():N}"),
                        new XElement(DcNs + "title", bookTitle),
                        new XElement(DcNs + "language", "en"),
                        new XElement(OpfNs + "meta",
                            new XAttribute("property", "dcterms:modified"),
                            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))),
                    new XElement(OpfNs + "manifest", manifestItems),
                    new XElement(OpfNs + "spine", spineItems)));
            opf.Save(Path.Combine(oebps, "content.opf"));

            var nav = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(XhtmlNs + "html",
                    new XAttribute(XNamespace.Xml + "lang", "en"),
                    new XAttribute("lang", "en"),
                    new XAttribute(XNamespace.Xmlns + "epub", "http://www.idpf.org/2007/ops"),
                    new XElement(XhtmlNs + "head",
                        new XElement(XhtmlNs + "title", bookTitle)),
                    new XElement(XhtmlNs + "body",
                        new XElement(XhtmlNs + "nav",
                            new XAttribute(XName.Get("type", "http://www.idpf.org/2007/ops"), "toc"),
                            new XElement(XhtmlNs + "h1", "Contents"),
                            new XElement(XhtmlNs + "ol", navLis)))));
            nav.Save(Path.Combine(oebps, "nav.xhtml"));

            ct.ThrowIfCancellationRequested();
            directoryService.ExistOrCreate(Path.GetDirectoryName(outputEpubPath)!);
            if (File.Exists(outputEpubPath)) File.Delete(outputEpubPath);

            using (var zip = ZipFile.Open(outputEpubPath, ZipArchiveMode.Create))
            {
                // EPUB requires uncompressed mimetype as first entry.
                zip.CreateEntryFromFile(Path.Combine(stagingDir, "mimetype"), "mimetype",
                    CompressionLevel.NoCompression);
                AddDirectoryToZip(zip, metaInf, "META-INF");
                AddDirectoryToZip(zip, oebps, "OEBPS");
            }

            logger.LogDebug("Converted {Archive} to EPUB with {PageCount} pages → {Output}",
                archivePath, images.Count, outputEpubPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(workRoot))
                {
                    Directory.Delete(workRoot, recursive: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to clean Kobo convert temp dir {WorkRoot}", workRoot);
            }
        }
    }

    private static void AddDirectoryToZip(ZipArchive zip, string sourceDir, string entryPrefix)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
            zip.CreateEntryFromFile(file, $"{entryPrefix}/{relative}", CompressionLevel.Optimal);
        }
    }

    private static string MediaTypeForExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".bmp" => "image/bmp",
        _ => "image/jpeg",
    };

    private static string EscapeXml(string value) =>
        new XText(value).ToString();
}
