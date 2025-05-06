using System;

namespace API.DTOs.KavitaPlus.Manage;

public sealed record ManageMatchSeriesDto
{
    public SeriesDto Series { get; set; }
    public bool IsMatched { get; set; }
    public DateTime ValidUntilUtc { get; set; }
}
