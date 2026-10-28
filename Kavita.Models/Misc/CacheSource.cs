namespace Kavita.Models.Misc;

public enum CacheSource
{
    /// <summary>
    /// Fetched on this call
    /// </summary>
    Live,
    /// <summary>
    /// Cached copy within its TTL, nothing was fetched
    /// </summary>
    Cache,
    /// <summary>
    /// Cached copy past its TTL, served because the fetch failed
    /// </summary>
    StaleCache,
    /// <summary>
    /// The fetch failed and nothing was cached
    /// </summary>
    Unavailable
}
