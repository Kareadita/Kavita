namespace API.DTOs.Dashboard;

public class UpdateStreamPositionDto
{
    public int FromPosition { get; set; }
    public int ToPosition { get; set; }
    public int Id { get; set; }
    public string StreamName { get; set; }
}
