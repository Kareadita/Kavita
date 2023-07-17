namespace API.DTOs.Stats;

/// <summary>
/// This is just for the Server tab on UI
/// </summary>
public class ServerInfoSlimDto
{
    /// <summary>
    /// Unique Id that represents a unique install
    /// </summary>
    public required string InstallId { get; set; }
    /// <summary>
    /// If the Kavita install is using Docker
    /// </summary>
    public bool IsDocker { get; set; }
    /// <summary>
    /// Version of Kavita
    /// </summary>
    public required string KavitaVersion { get; set; }

}
