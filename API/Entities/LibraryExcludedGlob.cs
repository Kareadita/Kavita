namespace API.Entities;

public class LibraryExcludedGlob
{
    public int Id { get; set; }
    public string Glob { get; set; }

    public int LibraryId { get; set; }
    public Library Library { get; set; } = null!;
}
