using System.Collections.Generic;

namespace API.DTOs.Email;

public class EmailOptionsDto
{
    public IList<string> ToEmails { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public IList<KeyValuePair<string, string>> PlaceHolders { get; set; }
}
