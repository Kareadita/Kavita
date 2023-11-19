namespace API.Entities;

public class LibraryExcludePattern
{
    public int Id { get; set; }
    public string Pattern { get; set; }

    public int LibraryId { get; set; }
    public Library Library { get; set; } = null!;
}
