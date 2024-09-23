namespace API.DTOs.Statistics;
#nullable enable

public class TopReadDto
{
    public int UserId { get; set; }
    public string? Username { get; set; } = default!;
    /// <summary>
    /// Amount of time read on Comic libraries
    /// </summary>
    public float ComicsTime { get; set; }
    /// <summary>
    /// Amount of time read on
    /// </summary>
    public float BooksTime { get; set; }
    public float MangaTime { get; set; }
}

