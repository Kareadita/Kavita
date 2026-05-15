using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditSeriesInfoDto
{
    public int SeriesId { get; init; }
    public string SeriesName { get; init; } = string.Empty;
    public bool IsMatched { get; init; }
    public long? MangaBakaId { get; init; }
    public DateTime? LastRefreshedUtc { get; init; }
    public IList<KavitaPlusAuditEntryDto> RecentEvents { get; init; } = [];
}
