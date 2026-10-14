namespace Kavita.Models.DTOs;

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
    /// Folder to scan, inside a library root. A series folder, or any folder inside one, scans that series. A folder
    /// holding several series (a publisher folder), a library root, or a folder no series owns yet scans the library.
    /// A path in no library is ignored
    /// </summary>
    /// <remarks>Forward slashes work on every OS. In JSON a backslash must be escaped: <c>"M:\\Manga\\Accel World"</c></remarks>
    public string FolderPath { get; set; } = default!;

    /// <summary>
    /// If true, only a series scan is ever run: when no single series owns <see cref="FolderPath"/>, nothing is scanned
    /// instead of the library. A folder that is the top folder of exactly one series counts as owned by it
    /// </summary>
    public bool AbortOnNoSeriesMatch { get; set; } = false;
}
