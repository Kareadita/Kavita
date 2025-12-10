using System;
using Kavita.Common.EnvironmentInfo;

namespace API.Entities.History;

/// <summary>
/// This will track manual migrations so that I can use simple selects to check if a Manual Migration is needed
/// </summary>
public class ManualMigrationHistory
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string ProductVersion { get; set; } = BuildInfo.Version.ToString();
    public DateTime RanAt { get; set; } = DateTime.UtcNow;
}
