using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Search;

public sealed record SearchResultDto
{
    public int SeriesId { get; init; }
    public string Name { get; init; } = default!;
    public string OriginalName { get; init; } = default!;
    public string SortName { get; init; } = default!;
    public string LocalizedName { get; init; } = default!;
    public MangaFormat Format { get; init; }

    // Grouping information
    public string LibraryName { get; set; } = default!;
    public int LibraryId { get; set; }
}
