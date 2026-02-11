using System.Collections.Generic;

namespace Kavita.Models.DTOs.Statistics;

public sealed record SpreadStatsDto
{
    public List<StatBucketDto> Buckets { get; set; }
    public int TotalCount { get; set; }
}
