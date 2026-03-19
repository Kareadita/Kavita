namespace Kavita.Models.DTOs.ReadingLists.CBL;

/// <summary>
/// Indicates which matching strategy resolved a CBL item to a Kavita entity.
/// Lower values indicate higher confidence.
/// </summary>
public enum CblMatchTier
{
    /// <summary>
    /// Matched via a user or admin remap rule
    /// </summary>
    RemapRule = 0,
    /// <summary>
    /// Matched via external database ID (ComicVine, Metron, etc.)
    /// </summary>
    ExternalId = 1,
    /// <summary>
    /// Matched via exact normalized name
    /// </summary>
    ExactName = 2,
    /// <summary>
    /// Comic Vine naming pattern: Series (VolumeNumber)
    /// </summary>
    ComicVineNaming = 3,
    /// <summary>
    /// Matched after stripping leading articles (The, A, An, etc.)
    /// </summary>
    ArticleStripped = 4,
    /// <summary>
    /// Matched after stripping reprint/edition suffixes
    /// </summary>
    ReprintStripped = 5,
    /// <summary>
    /// Matched via the AlternateSeries field on a chapter
    /// </summary>
    AlternateSeries = 6,
    /// <summary>
    /// Resolved by explicit user decision
    /// </summary>
    UserDecision = 7,
    /// <summary>
    /// Could not be matched to any Kavita entity
    /// </summary>
    Unmatched = -1
}
