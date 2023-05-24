using System.ComponentModel;

namespace API.DTOs.Scrobbling;

public enum ScrobbleEvent
{
    [Description("Chapter Read")]
    ChapterRead = 0,
    [Description("Add to Want to Read")]
    AddWantToRead = 1,
    [Description("Remove from Want to Read")]
    RemoveWantToRead = 2,
    [Description("Score Updated")]
    ScoreUpdated = 3
}


public class ScrobbleDto
{
    /// <summary>
    /// User's access token to allow us to talk on their behalf
    /// </summary>
    public string AccessToken { get; set; }

    public string SeriesName { get; set; }
    public string LocalizedSeriesName { get; set; }
    /// <summary>
    /// Optional AniListId if present on Kavita's WebLinks
    /// </summary>
    public int AniListId { get; set; } = 0;

    public ScrobbleEvent ScrobbleEvent { get; set; }
    /// <summary>
    /// Number of chapters read
    /// </summary>
    /// <remarks>If completed series, this can consider the Series Read (AniList)</remarks>
    public int ChapterNumber { get; set; }
    /// <summary>
    /// Number of Volumes read
    /// </summary>
    /// <remarks>This will not consider the series Completed, even if all Volumes have been read (AniList)</remarks>
    public int? VolumeNumber { get; set; }
    /// <summary>
    /// Rating for the Series
    /// </summary>
    public float? Rating { get; set; }

}
