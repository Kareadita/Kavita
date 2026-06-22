using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.KavitaPlus.License;

public sealed record KavitaPlusLicenseUsageDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public IReadOnlyList<ApiUsageDto> Stats { get; set; }
}

public sealed record ApiUsageDto
{
    public KavitaPlusApiName ApiName { get; set; }
    public long LifetimeCount { get; set; }
    public long Last30DaysCount { get; set; }
    public IReadOnlyList<DailyBucketDto> DailyBuckets { get; set; } = [];
}

public sealed record DailyBucketDto
{
    public DateOnly Date { get; set; }
    public long Count { get; set; }
}

public enum KavitaPlusApiName
{
    CoverRequests   = 1,
    MetadataSync    = 2,
    SeriesMatched   = 3,
    Scrobbles       = 4,
    MalStackImport  = 5,
    WantToRead      = 6,
    Recommendations = 7,
    Reviews = 8,
}
