using System;

namespace API.Entities;

public class SecurityEvent
{
    public int Id { get; set; }
    public string IpAddress { get; set; }
    public string RequestMethod { get; set; }
    public string RequestPath { get; set; }
    public string UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
