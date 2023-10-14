using System;

namespace API.DTOs.SideNav;

public class ExternalSourceDto
{
    public required int Id { get; set; } = 0;
    public required string Name { get; set; }
    public required string Host { get; set; }
    public required string ApiKey { get; set; }
}
