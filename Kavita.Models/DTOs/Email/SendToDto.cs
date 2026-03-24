using System.Collections.Generic;

namespace Kavita.Models.DTOs.Email;

public sealed record SendToDto
{
    public required int UserId { get; set; }
    public string DestinationEmail { get; set; } = default!;
    public IEnumerable<string> FilePaths { get; set; } = default!;
}
