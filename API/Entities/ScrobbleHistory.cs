using System;
using API.Services.Plus;

namespace API.Entities;

/// <summary>
/// Represents when Scrobble processing took place
/// </summary>
public class ScrobbleHistory
{
    public int Id { get; set; }
    public ScrobbleProvider Provider { get; set; }
    public DateTime LastRanUtc { get; set; }

}
