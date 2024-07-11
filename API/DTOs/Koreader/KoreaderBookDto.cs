namespace API.DTOs.Koreader;

public class KoreaderBookDto
{
    public string Document { get; set; }
    public string Device_id { get; set; }
    public string Device { get; set; }
    public float Percentage { get; set; }
    public string Progress { get; set; }
}
