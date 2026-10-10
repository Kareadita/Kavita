namespace Kavita.Services.Scanner;

/// <param name="LibraryId">Null for every library, and for a <see cref="ScannerService.ScanSeries"/> job, which does not carry it</param>
/// <param name="SeriesId">Set for a series scan</param>
public sealed record ScanTarget(int? LibraryId, int? SeriesId)
{
    public static readonly ScanTarget AllLibraries = new(null, null);

    public static ScanTarget Library(int libraryId) => new(libraryId, null);
    public static ScanTarget Series(int? libraryId, int seriesId) => new(libraryId, seriesId);

    /// <summary>
    /// A scan of this target also reads everything a scan of <paramref name="other"/> would
    /// </summary>
    public bool Covers(ScanTarget other)
    {
        if (SeriesId.HasValue) return SeriesId == other.SeriesId;
        if (LibraryId.HasValue) return LibraryId == other.LibraryId;
        return true;
    }
}
