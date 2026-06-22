using Kavita.Models.Entities.Enums.ReadingList;

namespace Kavita.Models.DTOs.ReadingLists.CBL;
#nullable enable

/// <summary>
/// Request body for the finalize-import endpoint
/// </summary>
public sealed record CblFinalizeRequestDto
{
    public string FileName { get; set; } = string.Empty;
    public CblImportDecisions Decisions { get; set; } = new();
    /// <summary>
    /// Import source type (File, Url, or None)
    /// </summary>
    public ReadingListProvider Provider { get; set; } = ReadingListProvider.None;
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

    /// <summary>
    /// Optional flag to promote the RL on creation
    /// </summary>
    public bool Promote { get; set; } = false;
}
