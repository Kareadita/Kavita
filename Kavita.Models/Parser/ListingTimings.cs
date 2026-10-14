using System.Collections.Generic;

namespace Kavita.Models.Parser;
#nullable enable

/// <summary>
/// Where the time went while building a <see cref="FolderTree"/>, in <see cref="System.Diagnostics.Stopwatch"/> ticks
/// </summary>
public sealed class ListingTimings
{
    public long EntryTicks { get; set; }
    public long StampTicks { get; set; }
    public long EventTicks { get; set; }
    public long SlowestTicks { get; set; }
    public string? SlowestFolder { get; set; }
    public List<double> FolderMs { get; } = [];
}
