namespace API.DTOs.Reader;

public class CreatePersonalToCDto
{
    public required int ChapterId { get; set; }
    public required int VolumeId { get; set; }
    public required int SeriesId { get; set; }
    public required int LibraryId { get; set; }
    public required int PageNumber { get; set; }
    public required string Title { get; set; }
    public string? BookScrollId { get; set; }
}
