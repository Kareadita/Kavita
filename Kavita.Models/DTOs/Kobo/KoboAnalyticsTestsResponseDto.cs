using System;

namespace Kavita.Models.DTOs.Kobo;

public class KoboAnalyticsTestsResponseDto
{
    public string Result { get; set; } = "Success";
    public string TestKey { get; set; } = Guid.NewGuid().ToString();
    public object Tests { get; set; } = new();
}
