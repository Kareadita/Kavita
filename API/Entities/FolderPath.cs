
using System;

namespace API.Entities;

public class FolderPath
{
    public int Id { get; set; }
    public required string Path { get; set; }
    /// <summary>
    /// Used when scanning to see if we can skip if nothing has changed
    /// </summary>
    /// <remarks>Time stored in UTC</remarks>
    public DateTime LastScanned { get; set; }

    // Relationship
    public Library Library { get; set; } = null!;
    public int LibraryId { get; set; }

    public void UpdateLastScanned(DateTime? time)
    {
        if (time == null)
        {
            LastScanned = DateTime.Now;
        }
        else
        {
            LastScanned = (DateTime) time;
        }
    }
}
