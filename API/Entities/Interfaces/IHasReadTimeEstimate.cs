using API.Services;

namespace API.Entities.Interfaces;

/// <summary>
/// Entity has read time estimate properties to estimate time to read
/// </summary>
public interface IHasReadTimeEstimate
{
    /// <summary>
    /// Min hours to read the chapter
    /// </summary>
    /// <remarks>Uses a fixed number to calculate from <see cref="ReaderService"/></remarks>
    public int MinHoursToRead { get; set; }
    /// <summary>
    /// Max hours to read the chapter
    /// </summary>
    /// <remarks>Uses a fixed number to calculate from <see cref="ReaderService"/></remarks>
    public int MaxHoursToRead { get; set; }
    /// <summary>
    /// Average hours to read the chapter
    /// </summary>
    /// <remarks>Uses a fixed number to calculate from <see cref="ReaderService"/></remarks>
    public float AvgHoursToRead { get; set; }
}
