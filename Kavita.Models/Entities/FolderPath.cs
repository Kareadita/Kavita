
using System;

namespace Kavita.Models.Entities;

public class FolderPath
{
    public int Id { get; set; }
    public required string Path { get; set; }
    /// <summary>
    /// Set at the end of each library scan, in server local time. Nothing reads it: change detection uses Series.LastScanned
    /// </summary>
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
