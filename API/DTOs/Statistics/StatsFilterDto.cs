using System;
using System.Collections.Generic;

namespace API.DTOs.Statistics;
#nullable enable

public sealed record StatsFilterDto
{
    public DateTime? StartDate { get; set; }
    /// <summary>
    /// Timezone of the user, will zone to this TimeZone
    /// </summary>
    public string? TimeZoneId { get; set; } // e.g., "America/Los_Angeles"

    public DateTime? EndDate
    {
        get;
        set => field = value == null || value == DateTime.MinValue ? DateTime.MaxValue : value;
    }


    private IList<int>? _libraries;
    public IList<int> Libraries
    {
        get => _libraries ?? [];
        set => _libraries = value;
    }

}
