using System.Xml.Serialization;

namespace API.DTOs.OPDS;

public class FeedCategory
{
    [XmlAttribute("scheme")]
    public string Scheme { get; } = "http://www.bisg.org/standards/bisac_subject/index.html";

    [XmlAttribute("term")]
    public string Term { get; set; }

    /// <summary>
    /// The actual genre
    /// </summary>
    [XmlAttribute("label")]
    public string Label { get; set; }
}
