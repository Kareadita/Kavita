namespace Kavita.Models.DTOs.KavitaPlus.ExternalMetadata.Covers;
#nullable enable

public enum ExternalCoverImageType
{
    Series,
    Volume,
    VolumeBack,
    Chapter,
    Issue,
    Banner,
    Season,
    Audiobook,
    Other
}

public sealed record ExternalCoverResponseDto
{
    public required string Url { get; set; } = string.Empty;
    public ExternalCoverImageType? Type { get; set; }
    /// <summary>
    /// Represents Volume or Chapter Number
    /// </summary>
    public float? Number { get; set; }
    public string? Language { get; set; }
}
