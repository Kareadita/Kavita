namespace API.DTOs.Filtering.v2;

/// <summary>
/// Represents the field which will dictate the value type and the Extension used for filtering
/// </summary>
public enum FilterField
{
    Summary = 0,
    SeriesName = 1,
    PublicationStatus = 2,
    Languages = 3,
    AgeRating = 4,
    UserRating = 5,
    Tags = 6,
    CollectionTags = 7,
    Translators = 8,
    Characters = 9,
    Publisher = 10,
    Editor = 11,
    CoverArtist = 12,
    Letterer = 13,
    Colorist = 14,
    Inker = 15,
    Penciller = 16,
    Writers = 17,
    Genres = 18,
    Libraries = 19,
    ReadProgress = 20,
    Formats = 21,
    ReleaseYear = 22,
    ReadTime = 23,
    /// <summary>
    /// Series Folder
    /// </summary>
    Path = 24,
    /// <summary>
    /// File path
    /// </summary>
    FilePath = 25,
    /// <summary>
    /// On Want To Read or Not
    /// </summary>
    WantToRead = 26,
    /// <summary>
    /// Last time User Read
    /// </summary>
    ReadingDate = 27,
    /// <summary>
    /// Average rating from Kavita+ - Not usable for non-licensed users
    /// </summary>
    AverageRating = 28,
    Imprint = 29,
    Team = 30,
    Location = 31,
    /// <summary>
    /// Last time User Read
    /// </summary>
    ReadLast = 32,

}
