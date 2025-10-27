namespace API.DTOs.Dashboard;

public sealed record UpdateStreamPositionDto
{
    public string StreamName { get; set; }
    public int Id { get; set; }
    public int FromPosition { get; set; }
    public int ToPosition { get; set; }
    /// <summary>
    /// If the <see cref="ToPosition"/> has taken into account non-visible items
    /// </summary>
    public bool PositionIncludesInvisible { get; set; }
}
