namespace API.DTOs.JumpBar;

/// <summary>
/// Represents an individual button in a Jump Bar
/// </summary>
public class JumpKeyDto
{
    /// <summary>
    /// Number of items in this Key
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Code to use in URL (url encoded)
    /// </summary>
    public string Key { get; set; } = default!;
    /// <summary>
    /// What is visible to user
    /// </summary>
    public string Title { get; set; } = default!;
}
