using System;
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

    /// <summary>
    /// lastRead MUST provide the last page read for this document. The numbering starts at 1.
    /// </summary>
    [XmlAttribute("lastRead", Namespace = "http://vaemendis.net/opds-pse/ns")]
    public int LastRead { get; set; }

    /// <summary>
    /// lastReadDate MAY provide the date of when the lastRead attribute was last updated.
    /// </summary>
    /// <remarks>Attribute MUST conform Atom's Date construct</remarks>
    [XmlAttribute("lastReadDate", Namespace = "http://vaemendis.net/opds-pse/ns")]
    public DateTime LastReadDate { get; set; }

    public bool ShouldSerializeTotalPages()
    {
        return TotalPages > 0;
    }
}
