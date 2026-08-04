using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Kavita.API.Services;

namespace Kavita.Services.Kobo;

/// <summary>
/// Pure PagesRead ↔ factual Kobo Location codec for CBZ/CBR convert KEPUB artifacts.
/// Encode/decode only; callers decide when KEPUB is device-openable and page count is trusted.
/// </summary>
public static partial class KoboConvertLocationCodec
{
    public const string TypeKoboSpan = KoboLocationMapper.TypeKoboSpan;
    public const string ValueKoboSpan = "kobo.1.1";
    public const string PageSourceDirectory = "OEBPS/Text";

    [GeneratedRegex(@"^page_(\d{4,})\.xhtml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PageFileNameRegex();

    /// <summary>
    /// Encodes 0-based <paramref name="pagesRead"/> to a factual convert Location, or null to omit/clear.
    /// ReadyToRead / no progress → null when <paramref name="readyToRead"/> is true;
    /// in progress (<c>0 ≤ pagesRead &lt; totalPages</c>) → <c>page_{pagesRead+1}</c>;
    /// finished (<c>pagesRead ≥ totalPages</c>) → last page.
    /// </summary>
    public static KoboMappedLocation? TryEncode(int pagesRead, int totalPages, bool readyToRead = false)
    {
        if (totalPages <= 0) return null;
        if (readyToRead) return null;
        if (pagesRead < 0) return null;

        var oneBased = pagesRead >= totalPages ? totalPages : pagesRead + 1;
        return new KoboMappedLocation(ValueKoboSpan, TypeKoboSpan, FormatPageSource(oneBased));
    }

    /// <summary>
    /// Decodes a convert Location to <paramref name="pagesRead"/> in <c>[0, totalPages]</c>.
    /// Page <c>N</c> of <c>N</c> maps to <c>totalPages</c> (finished), matching
    /// <see cref="TryEncode"/> for <c>pagesRead &gt;= totalPages</c>. Earlier pages map to
    /// 0-based indices. Fail-closed on bad Type/Value/Source/range.
    /// </summary>
    /// <remarks>
    /// Encode maps both <c>totalPages - 1</c> (on last page) and <c>totalPages</c> (finished)
    /// to the same last-page Source; decode prefers finished so finished round-trips.
    /// </remarks>
    public static bool TryDecode(string? locationValue, string? locationType, string? locationSource,
        int totalPages, out int pagesRead)
    {
        pagesRead = 0;
        if (totalPages <= 0) return false;
        if (!string.Equals(locationType, TypeKoboSpan, StringComparison.Ordinal)) return false;
        if (!string.Equals(locationValue, ValueKoboSpan, StringComparison.Ordinal)) return false;
        if (!TryParsePageNumber(locationSource, out var oneBased)) return false;
        if (oneBased < 1 || oneBased > totalPages) return false;

        // Last page document ↔ finished (totalPages); earlier pages ↔ 0-based index.
        pagesRead = oneBased == totalPages
            ? totalPages
            : Math.Clamp(oneBased - 1, 0, totalPages - 1);
        return true;
    }

    public static string FormatPageFileName(int oneBasedPage) =>
        $"page_{oneBasedPage.ToString("D4", CultureInfo.InvariantCulture)}.xhtml";

    public static string FormatPageSource(int oneBasedPage) =>
        $"{PageSourceDirectory}/{FormatPageFileName(oneBasedPage)}";

    /// <summary>
    /// Accepts full <c>OEBPS/Text/page_NNNN.xhtml</c>, a suffix of that path, or basename only.
    /// </summary>
    public static bool TryParsePageNumber(string? locationSource, out int oneBasedPage)
    {
        oneBasedPage = 0;
        if (string.IsNullOrWhiteSpace(locationSource)) return false;

        var normalized = locationSource.Replace('\\', '/').Trim();
        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(fileName)) return false;

        var match = PageFileNameRegex().Match(fileName);
        if (!match.Success) return false;

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                out oneBasedPage))
        {
            return false;
        }

        return oneBasedPage >= 1;
    }
}
