namespace Kavita.Models.DTOs.KavitaPlus.Metadata;

public sealed record ExternalEditionDto
{
    /// <summary>
    /// The ID of the edition.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// The title of the edition.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// The format of the edition.
    /// </summary>
    public required string Format { get; set; }

    /// <summary>
    /// The language of the edition.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The publisher of the edition.
    /// </summary>
    public string? Publisher { get; set; }
}
