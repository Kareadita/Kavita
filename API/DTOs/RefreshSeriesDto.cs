namespace API.DTOs;

/// <summary>
/// Used for running some task against a Series.
/// </summary>
public class RefreshSeriesDto
{
    /// <summary>
    /// Library Id series belongs to
    /// </summary>
    public int LibraryId { get; init; }
    /// <summary>
    /// Series Id
    /// </summary>
    public int SeriesId { get; init; }
    /// <summary>
    /// Should the task force opening/re-calculation.
    /// </summary>
    /// <remarks>This is expensive if true. Defaults to true.</remarks>
    public bool ForceUpdate { get; init; } = true;
    /// <summary>
    /// Should the task force re-calculation of colorscape.
    /// </summary>
    /// <remarks>This is expensive if true. Defaults to true.</remarks>
    public bool ForceColorscape { get; init; } = false;
}
