namespace Kavita.Models.Entities.MetadataMatching;

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
    Name = 10,
    #endregion

    #region Chapter Metadata

    ChapterTitle = 20,
    ChapterSummary = 21,
    ChapterReleaseDate = 22,
    ChapterPublisher = 23,
    ChapterCovers = 24,
    ChapterAgeRating = 25,

    #endregion

    #region Volume Metadata

    VolumeCovers = 40,

    #endregion


}
