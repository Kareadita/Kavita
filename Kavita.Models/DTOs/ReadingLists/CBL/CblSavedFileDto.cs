namespace Kavita.Models.DTOs.ReadingLists.CBL;
#nullable enable

/// <summary>
/// Response for save-only CBL upload endpoints
/// </summary>
public record CblSavedFileDto
{
    /// <summary>
    /// Display name (filename for file/url, repo item name for repo)
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Server-side filename in cbl-manager-download/
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    /// <summary>
    /// Repo-relative path (null for file/URL sources)
    /// </summary>
    public string? RepoPath { get; set; }
    /// <summary>
    /// Cached download URL (null for file/URL sources)
    /// </summary>
    public string? DownloadUrl { get; set; }
    /// <summary>
    /// Git SHA for change detection (null for file/URL sources)
    /// </summary>
    public string? Sha { get; set; }
}
