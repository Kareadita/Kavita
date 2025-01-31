namespace API.DTOs.Recommendation;
#nullable enable

public class SeriesStaffDto
{
    public required string Name { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public required string Url { get; set; }
    public required string Role { get; set; }
    public string? ImageUrl { get; set; }
    public string? Gender { get; set; }
    public string? Description { get; set; }
}
