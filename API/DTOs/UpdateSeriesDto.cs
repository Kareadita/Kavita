namespace API.DTOs;

public class UpdateSeriesDto
{
    public int Id { get; init; }
    public string? LocalizedName { get; init; }
    public string? SortName { get; init; }
    public bool CoverImageLocked { get; set; }

    public bool SortNameLocked { get; set; }
    public bool LocalizedNameLocked { get; set; }
}
