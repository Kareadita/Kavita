using System.Xml.Serialization;

namespace API.DTOs.OPDS;

public class SearchLink
{
    [XmlAttribute("type")]
    public string Type { get; set; } = default!;

    [XmlAttribute("rel")]
    public string Rel { get; set; } = "results";

    [XmlAttribute("template")]
    public string Template { get; set; } = default!;
}
