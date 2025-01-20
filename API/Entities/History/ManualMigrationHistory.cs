using System;

namespace API.Entities.History;

/// <summary>
/// This will track manual migrations so that I can use simple selects to check if a Manual Migration is needed
/// </summary>
public class ManualMigrationHistory
{
    public int Id { get; set; }
    public string ProductVersion { get; set; }
    public required string Name { get; set; }
    public DateTime RanAt { get; set; }
}
