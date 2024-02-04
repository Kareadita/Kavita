using System;

namespace API.Entities.Metadata;

/// <summary>
/// A blacklist of Series for Kavita+
/// </summary>
public class SeriesBlacklist
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public Series Series { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}
