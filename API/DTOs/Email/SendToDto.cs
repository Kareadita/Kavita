using System.Collections.Generic;

namespace API.DTOs.Email;

public class SendToDto
{
    public string DestinationEmail { get; set; }
    public IEnumerable<string> FilePaths { get; set; }
}
