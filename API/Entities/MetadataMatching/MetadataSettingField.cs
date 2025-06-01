namespace API.Entities.MetadataMatching;

/// <summary>
/// Represents which field that can be written to as an override when already locked
/// </summary>
public enum MetadataSettingField
{
    #region Series Metadata
    Summary = 1,
    PublicationStatus = 2,
    StartDate = 3,
    Genres = 4,
    Tags = 5,
    LocalizedName = 6,
    Covers = 7,
    AgeRating = 8,
    People = 9,
    #endregion

    #region Chapter Metadata

    ChapterTitle = 10,
    ChapterSummary = 11,
    ChapterReleaseDate = 12,
    ChapterPublisher = 13,
    ChapterCovers = 14,

    #endregion


}
