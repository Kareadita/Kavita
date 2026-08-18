namespace Kavita.Models.DTOs.KavitaPlus.Manage;

public sealed record MatchedExternalSeriesCountDto
{
    public int TotalCount { get; set; }
    public int DontMatchCount { get; set; }
    public int NotMatchedCount { get; set; }
    public int ErroredCount { get; set; }
}

