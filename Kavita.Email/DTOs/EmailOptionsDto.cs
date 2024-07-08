using System.Collections.Generic;

namespace Skeleton.DTOs;

public class EmailOptionsDto
{
    public IList<string> ToEmails { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public IList<KeyValuePair<string, string>> PlaceHolders { get; set; }
    /// <summary>
    /// Filenames to attach
    /// </summary>
    public IList<string> Attachments { get; set; }
}