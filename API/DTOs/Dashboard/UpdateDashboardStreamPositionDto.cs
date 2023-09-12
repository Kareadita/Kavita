namespace API.DTOs.Dashboard;

public class UpdateDashboardStreamPositionDto
{
    public int FromPosition { get; set; }
    public int ToPosition { get; set; }
    public int DashboardStreamId { get; set; }
    public string StreamName { get; set; }
}
