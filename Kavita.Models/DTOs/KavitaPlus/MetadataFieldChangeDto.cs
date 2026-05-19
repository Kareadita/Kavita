namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

/// <summary>
/// Records a single field's before/after state during a metadata write
/// </summary>
public sealed record MetadataFieldChangeDto(MetadataFieldChangeKind Field, object? From, object? To);

/// <summary>
/// Represents individual fields for any entity type. Will be localized in the UI layer.
/// </summary>
public enum MetadataFieldChangeKind
{
    Relationships = 1,
    Characters = 2,
    Artists = 3,
    Writers = 4,
    Tags = 5,
    Genres = 6,
    PublicationStatus = 7,
    AgeRating = 8,
    ExternalIds = 9,
    Summary = 10,
    Title = 11,
    ReleaseDate = 12,
    ReleaseYear = 13,
    LocalizedName = 14
}
