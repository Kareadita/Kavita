using System;

namespace API.Entities.Metadata;

/// <summary>
/// A blacklist of Series for Kavita+
/// </summary>
[Obsolete("Kavita v0.8.5 moved the implementation to Series.IsBlacklisted")]
public class SeriesBlacklist
{
    public int Id { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;

    public int SeriesId { get; set; }
    public Series Series { get; set; }
}
