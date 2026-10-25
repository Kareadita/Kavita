
namespace Kavita.Models.Entities;

public class FolderPath
{
    public int Id { get; set; }
    public required string Path { get; set; }

    // Relationship
    public Library Library { get; set; } = null!;
    public int LibraryId { get; set; }
}
