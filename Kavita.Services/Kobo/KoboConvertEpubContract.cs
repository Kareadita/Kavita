using System.Xml.Linq;

namespace Kavita.Services.Kobo;

/// <summary>
/// Shared structural contract for the archive→EPUB convert artifact, used by
/// <see cref="KoboArchiveEpubConverter"/> (writer) and <see cref="KoboConvertEpubInspector"/> (reader).
/// Bump <see cref="KoboConversionService.ConvertContractVersion"/> when any value here changes so
/// EPUB/KEPUB cache fingerprints miss and old artifacts are orphaned.
/// </summary>
public static class KoboConvertEpubContract
{
    public static readonly XNamespace OpfNs = "http://www.idpf.org/2007/opf";
    public static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
    public static readonly XNamespace XhtmlNs = "http://www.w3.org/1999/xhtml";
    public static readonly XNamespace ContainerNs = "urn:oasis:names:tc:opendocument:xmlns:container";

    public const string MimeType = "application/epub+zip";

    public const string OebpsFolder = "OEBPS";
    public const string TextFolder = "Text";
    public const string ImagesFolder = "Images";

    public const string MimeTypeEntry = "mimetype";
    public const string ContainerPath = "META-INF/container.xml";
    public const string OpfPath = "OEBPS/content.opf";

    public const string ContainerXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles>
            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    /// <summary>Spine content-doc id for the 1-based page index, e.g. <c>page_0001</c>.</summary>
    public static string PageId(int oneBasedIndex) => $"page_{oneBasedIndex:D4}";

    /// <summary>Image manifest id for the 1-based page index, e.g. <c>img_0001</c>.</summary>
    public static string ImageId(int oneBasedIndex) => $"img_{oneBasedIndex:D4}";

    /// <summary>Content-doc file name for the 1-based page index, e.g. <c>page_0001.xhtml</c>.</summary>
    public static string PageFileName(int oneBasedIndex) => $"{PageId(oneBasedIndex)}.xhtml";
}
