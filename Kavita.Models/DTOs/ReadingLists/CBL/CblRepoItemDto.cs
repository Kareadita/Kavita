namespace Kavita.Models.DTOs.ReadingLists.CBL;
#nullable enable

public sealed record CblRepoItemDto
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public string Sha { get; init; } = string.Empty;
    public long Size { get; set; }
    public string? DownloadUrl { get; init; }

    public int? ExistingReadingListId { get; set; }
    public bool AlreadySynced => ExistingReadingListId.HasValue;
}
