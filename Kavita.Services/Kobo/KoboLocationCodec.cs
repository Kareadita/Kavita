using System;
using Kavita.API.Services;

namespace Kavita.Services.Kobo;

/// <summary>
/// Dispatch façade for Kobo Location codecs: convert (page-based) vs prose (via <see cref="IKoboLocationMapper"/>).
/// </summary>
public static class KoboLocationCodec
{
    public static bool IsConvertSentinel(string? locationValue) =>
        string.Equals(locationValue, KoboConvertLocationCodec.ValueKoboSpan, StringComparison.Ordinal);

    public static KoboMappedLocation? TryEncodeConvert(int pagesRead, int totalPages, bool readyToRead = false) =>
        KoboConvertLocationCodec.TryEncode(pagesRead, totalPages, readyToRead);

    public static bool TryDecodeConvert(string? locationValue, string? locationType, string? locationSource,
        int totalPages, out int pagesRead) =>
        KoboConvertLocationCodec.TryDecode(locationValue, locationType, locationSource, totalPages, out pagesRead);
}
