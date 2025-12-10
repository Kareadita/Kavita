using System;
using System.Collections.Generic;

namespace API.DTOs.Statistics;

public sealed record StatsFilterDto
{
    public DateTime? StartDate { get; set; }

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
