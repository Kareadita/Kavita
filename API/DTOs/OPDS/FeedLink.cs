using System.Xml.Serialization;

namespace API.DTOs.OPDS;

public class FeedLink
{
    /// <summary>
    /// Relation on the Link
    /// </summary>
    [XmlAttribute("rel")]
    public string Rel { get; set; }

    /// <summary>
    /// Should be any of the types here <see cref="FeedLinkType"/>
    /// </summary>
    [XmlAttribute("type")]
    public string Type { get; set; }

    [XmlAttribute("href")]
    public string Href { get; set; }

    [XmlAttribute("title")]
    public string Title { get; set; }

    [XmlAttribute("count", Namespace = "http://vaemendis.net/opds-pse/ns")]
    public int TotalPages { get; set; }

    public bool ShouldSerializeTotalPages()
    {
        return TotalPages > 0;
    }
}
