using System;

namespace API.DTOs.Koreader;

public class KoreaderProgressUpdateDto
{
    public string Document { get; set; }
    public DateTime Timestamp { get; set; }
}
