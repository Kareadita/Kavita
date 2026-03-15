using System.Collections.Generic;
using Kavita.Models.DTOs.Misc;

namespace Kavita.Models.DTOs.ReadingLists.CBL;
#nullable enable

public class CblRepoItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string Sha { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? DownloadUrl { get; set; }
}

public class CblRepoBrowseResultDto
{
    public IList<CblRepoItemDto> Items { get; set; } = [];
    public GithubRateLimitDto RateLimitDto { get; set; } = new();
    /// <summary>
    /// True if this result was served from cache (no GitHub API call made)
    /// </summary>
    public bool FromCache { get; set; }
}
