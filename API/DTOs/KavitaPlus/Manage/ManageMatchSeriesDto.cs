using System;

namespace API.DTOs.KavitaPlus.Manage;

public class ManageMatchSeriesDto
{
    public SeriesDto Series { get; set; }
    public bool IsMatched { get; set; }
    public DateTime ValidUntilUtc { get; set; }
}
