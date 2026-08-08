using System.ComponentModel.DataAnnotations;
namespace Kavita.Models.DTOs.KavitaPlus.Metadata;

public enum EditionEntryType
{
    Chapter = 0,
    Volume = 1,
    Other = 2,
}

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

    /// <summary>
    /// Which type of entries are counted
    /// </summary>
    [EnumDataType(typeof(EditionEntryType))]
    public EditionEntryType Type { get; set; }

    /// <summary>
    /// Number of entries in the main storyline
    /// </summary>
    public int MainCount { get; set; }

    /// <summary>
    /// Total number of entries (Includes extras, etc.)
    /// </summary>
    public int TotalCount { get; set; }
}