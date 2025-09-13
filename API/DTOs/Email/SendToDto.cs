using System.Collections.Generic;

namespace API.DTOs.Email;

public sealed record SendToDto
{
    public string DestinationEmail { get; set; } = default!;
    public IEnumerable<string> FilePaths { get; set; } = default!;
}
