using API.Entities.Enums;

namespace API.DTOs.Search;

public class SearchResultDto
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
