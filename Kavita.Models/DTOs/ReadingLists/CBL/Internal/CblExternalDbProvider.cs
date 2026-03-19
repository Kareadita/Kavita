namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// Known external comic database providers used for issue/series identification.
/// </summary>
public enum CblExternalDbProvider
{
    /// <summary>
    /// Comic Vine (comicvine.gamespot.com). Provider short-name: "cv"
    /// </summary>
    ComicVine,
    /// <summary>
    /// Metron (metron.cloud). Provider short-name: "metron"
    /// </summary>
    Metron,
    /// <summary>
    /// Grand Comics Database (comics.org). Provider short-name: "gcd"
    /// </summary>
    GrandComicsDatabase,
    /// <summary>
    /// Unrecognised or missing provider
    /// </summary>
    Unknown
}
