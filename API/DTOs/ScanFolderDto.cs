namespace API.DTOs;

/// <summary>
/// DTO for requesting a folder to be scanned
/// </summary>
public sealed record ScanFolderDto
{
    /// <summary>
    /// Api key for a user with Admin permissions
    /// </summary>
    public string ApiKey { get; set; } = default!;
    /// <summary>
    /// Folder Path to Scan
    /// </summary>
    /// <remarks>JSON cannot accept /, so you may need to use // escaping on paths</remarks>
    public string FolderPath { get; set; } = default!;

    /// <summary>
    /// If true, only runs the scan if a matches series is found. I.e. prevent library scans
    /// </summary>
    public bool AbortOnNoSeriesMatch { get; set; } = false;
}
