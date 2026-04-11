namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// Known external comic database providers used for issue/series identification.
/// </summary>
public enum CblExternalDbProvider
{
    /// <summary>
    /// Comic Vine (comicvine.gamespot.com)
    /// </summary>
    ComicVine,
    /// <summary>
    /// Metron (metron.cloud)
    /// </summary>
    Metron,
    /// <summary>
    /// Grand Comics Database (comics.org)
    /// </summary>
    GrandComicsDatabase,
    /// <summary>
    /// Kavita, unofficial
    /// </summary>
    Kavita,
    /// <summary>
    /// AniList, unofficial
    /// </summary>
    AniList,
    /// <summary>
    /// MyAnimeList, unofficial
    /// </summary>
    Mal,
    /// <summary>
    /// Hardcover, unofficial
    /// </summary>
    Hardcover,
    /// <summary>
    /// Unrecognised or missing provider
    /// </summary>
    Unknown
}
