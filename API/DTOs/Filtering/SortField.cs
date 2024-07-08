namespace API.DTOs.Filtering;

public enum SortField
{
    /// <summary>
    /// Sort Name of Series
    /// </summary>
    SortName = 1,
    /// <summary>
    /// Date entity was created/imported into Kavita
    /// </summary>
    CreatedDate = 2,
    /// <summary>
    /// Date entity was last modified (tag update, etc)
    /// </summary>
    LastModifiedDate = 3,
    /// <summary>
    /// Date series had a chapter added to it
    /// </summary>
    LastChapterAdded = 4,
    /// <summary>
    /// Time it takes to read. Uses Average.
    /// </summary>
    TimeToRead = 5,
    /// <summary>
    /// Release Year of the Series
    /// </summary>
    ReleaseYear = 6,
    /// <summary>
    /// Last time the user had any reading progress
    /// </summary>
    ReadProgress = 7,
    /// <summary>
    /// Kavita+ Only - External Average Rating
    /// </summary>
    AverageRating = 8,
    /// <summary>
    /// Randomise the order
    /// </summary>
    Random = 9
}
