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
    private static readonly XNamespace OpfNs = KoboConvertEpubContract.OpfNs;
    private static readonly XNamespace DcNs = KoboConvertEpubContract.DcNs;
    private static readonly XNamespace XhtmlNs = KoboConvertEpubContract.XhtmlNs;

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
            var images = ExtractAndOrderImages(archivePath, extractDir, ct);

            var metaInf = Path.Combine(stagingDir, "META-INF");
            var oebps = Path.Combine(stagingDir, KoboConvertEpubContract.OebpsFolder);
            var imagesDir = Path.Combine(oebps, KoboConvertEpubContract.ImagesFolder);
            var textDir = Path.Combine(oebps, KoboConvertEpubContract.TextFolder);
            directoryService.ExistOrCreate(metaInf);
            directoryService.ExistOrCreate(imagesDir);
            directoryService.ExistOrCreate(textDir);

            File.WriteAllText(Path.Combine(stagingDir, KoboConvertEpubContract.MimeTypeEntry),
                KoboConvertEpubContract.MimeType,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(Path.Combine(metaInf, "container.xml"), KoboConvertEpubContract.ContainerXml);

            var bookTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(archivePath) : title;
            var (manifestItems, spineItems, navLis) =
                BuildPageDocuments(images, imagesDir, textDir, bookTitle, ct);

            WriteOpfAndNav(oebps, bookTitle, manifestItems, spineItems, navLis);
            PackageEpub(stagingDir, metaInf, oebps, outputEpubPath, ct);

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

    private List<string> ExtractAndOrderImages(string archivePath, string extractDir, CancellationToken ct)
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

        return images;
    }

    /// <summary>
    /// Copies each image and writes its single-image content doc, returning the manifest (nav first),
    /// spine, and nav entries for the package.
    /// </summary>
    private static (List<XElement> Manifest, List<XElement> Spine, List<XElement> Nav) BuildPageDocuments(
        IReadOnlyList<string> images, string imagesDir, string textDir, string bookTitle, CancellationToken ct)
    {
        var manifestItems = new List<XElement>();
        var spineItems = new List<XElement>();
        var navLis = new List<XElement>();

        for (var i = 0; i < images.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var pageNumber = i + 1;
            var source = images[i];
            var ext = Path.GetExtension(source).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            var imageFileName = KoboConvertEpubContract.PageId(pageNumber) + ext;
            var pageFileName = KoboConvertEpubContract.PageFileName(pageNumber);
            var imageId = KoboConvertEpubContract.ImageId(pageNumber);
            var pageId = KoboConvertEpubContract.PageId(pageNumber);

            File.Copy(source, Path.Combine(imagesDir, imageFileName), overwrite: true);
            File.WriteAllText(Path.Combine(textDir, pageFileName),
                BuildPageHtml(bookTitle, imageFileName, pageNumber), Encoding.UTF8);

            manifestItems.Add(new XElement(OpfNs + "item",
                new XAttribute("id", imageId),
                new XAttribute("href", $"{KoboConvertEpubContract.ImagesFolder}/{imageFileName}"),
                new XAttribute("media-type", MediaTypeForExtension(ext))));
            manifestItems.Add(new XElement(OpfNs + "item",
                new XAttribute("id", pageId),
                new XAttribute("href", $"{KoboConvertEpubContract.TextFolder}/{pageFileName}"),
                new XAttribute("media-type", "application/xhtml+xml")));
            spineItems.Add(new XElement(OpfNs + "itemref", new XAttribute("idref", pageId)));
            navLis.Add(new XElement(XhtmlNs + "li",
                new XElement(XhtmlNs + "a",
                    new XAttribute("href", $"{KoboConvertEpubContract.TextFolder}/{pageFileName}"),
                    $"Page {pageNumber}")));
        }

        manifestItems.Insert(0, new XElement(OpfNs + "item",
            new XAttribute("id", "nav"),
            new XAttribute("href", "nav.xhtml"),
            new XAttribute("media-type", "application/xhtml+xml"),
            new XAttribute("properties", "nav")));

        return (manifestItems, spineItems, navLis);
    }

    private static void WriteOpfAndNav(string oebps, string bookTitle,
        IReadOnlyList<XElement> manifestItems, IReadOnlyList<XElement> spineItems, IReadOnlyList<XElement> navLis)
    {
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
    }

    private void PackageEpub(string stagingDir, string metaInf, string oebps, string outputEpubPath,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        directoryService.ExistOrCreate(Path.GetDirectoryName(outputEpubPath)!);
        if (File.Exists(outputEpubPath)) File.Delete(outputEpubPath);

        using var zip = ZipFile.Open(outputEpubPath, ZipArchiveMode.Create);
        // EPUB requires uncompressed mimetype as first entry.
        zip.CreateEntryFromFile(Path.Combine(stagingDir, KoboConvertEpubContract.MimeTypeEntry),
            KoboConvertEpubContract.MimeTypeEntry, CompressionLevel.NoCompression);
        AddDirectoryToZip(zip, metaInf, "META-INF");
        AddDirectoryToZip(zip, oebps, KoboConvertEpubContract.OebpsFolder);
    }

    private static string BuildPageHtml(string bookTitle, string imageFileName, int pageNumber) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<!DOCTYPE html>\n" +
        "<html xmlns=\"http://www.w3.org/1999/xhtml\">\n" +
        "<head>\n" +
        $"<title>{EscapeXml(bookTitle)} — {pageNumber}</title>\n" +
        "<meta charset=\"utf-8\"/>\n" +
        "<style type=\"text/css\">html, body { margin: 0; padding: 0; text-align: center; background: #000; } img { max-width: 100%; height: auto; }</style>\n" +
        "</head>\n" +
        "<body>\n" +
        $"<img src=\"../{KoboConvertEpubContract.ImagesFolder}/{imageFileName}\" alt=\"Page {pageNumber}\"/>\n" +
        "</body>\n" +
        "</html>\n";

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
