namespace Kavita.Models.Entities.Enums.KavitaPlus;

/// <summary>
/// Recommendation Source from Kavita+. Each recommendation has exactly one source; when the same series surfaces
/// from both lists, <see cref="UserBased"/> wins.
/// </summary>
public enum RecommendationSource
{
    /// <summary>
    /// Generic Recommendation based on the underlying Series
    /// </summary>
    Similar = 1,
    /// <summary>
    /// Personalized (Readers also Like) based on your reading activity with that provider
    /// </summary>
    UserBased = 2,
    /// <summary>
    /// Based on your actual reading history and ratings (only applicable if you scrobble with them)
    /// </summary>
    Personalized = 3

}
