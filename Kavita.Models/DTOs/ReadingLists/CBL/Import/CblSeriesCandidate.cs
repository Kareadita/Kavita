namespace Kavita.Models.DTOs.ReadingLists.CBL.Import;

/// <summary>
/// A candidate series for user disambiguation when multiple series match a CBL name
/// </summary>
public sealed record CblSeriesCandidate(int SeriesId, int LibraryId, string SeriesName);
