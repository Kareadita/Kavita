using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Kavita.Services.Kobo;

/// <summary>
/// Reads convert EPUB/KEPUB spine length for page-count trust checks.
/// </summary>
public static class KoboConvertEpubInspector
{
    private static readonly XNamespace OpfNs = KoboConvertEpubContract.OpfNs;
    private static readonly XNamespace ContainerNs = KoboConvertEpubContract.ContainerNs;

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

    private static string? ResolveOpfPath(ZipArchive zip)
    {
        var container = zip.GetEntry(KoboConvertEpubContract.ContainerPath);
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

        return zip.GetEntry(KoboConvertEpubContract.OpfPath) != null ? KoboConvertEpubContract.OpfPath : null;
    }
}
