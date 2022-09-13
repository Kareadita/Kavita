using API.Entities.Enums;

namespace API.DTOs.Search;

public class SearchResultDto
{
    public int SeriesId { get; init; }
    public string Name { get; init; }
    public string OriginalName { get; init; }
    public string SortName { get; init; }
    public string LocalizedName { get; init; }
    public MangaFormat Format { get; init; }

    // Grouping information
    public string LibraryName { get; set; }
    public int LibraryId { get; set; }
}
