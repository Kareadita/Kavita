namespace API.DTOs.Statistics;
#nullable enable

public class TopReadDto
{
    public int UserId { get; set; }
    public string? Username { get; set; } = default!;
    /// <summary>
    /// Amount of time read on Comic libraries
    /// </summary>
    public long ComicsTime { get; set; }
    /// <summary>
    /// Amount of time read on
    /// </summary>
    public long BooksTime { get; set; }
    public long MangaTime { get; set; }
}

