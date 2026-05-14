using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KavitaPlusAuditSeriesInfoDto(
    int SeriesId,
    string SeriesName,
    bool IsMatched,
    long? MangaBakaId,
    DateTime? LastRefreshedUtc,
    IList<KavitaPlusAuditEntryDto> RecentEvents
);
