namespace Kavita.Models.DTOs.ReadingLists.CBL;

/// <summary>
/// Request body for the finalize-import endpoint
/// </summary>
public record CblFinalizeRequestDto
{
    public string FileName { get; set; } = string.Empty;
    public CblImportDecisions Decisions { get; set; } = new();
    /// <summary>
    /// Optional repo-relative path for sync tracking
    /// </summary>
    public string? RepoPath { get; set; }
    /// <summary>
    /// Optional cached download URL for sync tracking
    /// </summary>
    public string? DownloadUrl { get; set; }
    /// <summary>
    /// Optional Git SHA for sync tracking
    /// </summary>
    public string? Sha { get; set; }
}
