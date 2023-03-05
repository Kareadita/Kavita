using System.Collections.Generic;

namespace API.DTOs;

public class UpdateRbsDto
{
    public required string Username { get; init; }
    public IList<string>? Roles { get; init; }
}
