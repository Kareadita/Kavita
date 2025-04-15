namespace API.Entities.MetadataMatching;

/// <summary>
/// Represents which field that can be written to as an override when already locked
/// </summary>
public enum MetadataSettingField
{
    Summary = 1,
    PublicationStatus = 2,
    StartDate = 3,
    Genres = 4,
    Tags = 5,
    LocalizedName = 6,
    Covers = 7,
    AgeRating = 8,
    People = 9
}
