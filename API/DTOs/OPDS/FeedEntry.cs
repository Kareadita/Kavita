using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace API.DTOs.OPDS;

public class FeedEntry
{
    [XmlElement("updated")]
    public string Updated { get; init; } = DateTime.UtcNow.ToString("s");

    [XmlElement("id")]
    public string Id { get; set; }

    [XmlElement("title")]
    public string Title { get; set; }

    [XmlElement("summary")]
    public string Summary { get; set; }

    /// <summary>
    /// Represents Size of the Entry
    /// Tag: , ElementName = "dcterms:extent"
    /// <example>2 MB</example>
    /// </summary>
    [XmlElement("extent", Namespace = "http://purl.org/dc/terms/")]
    public string Extent { get; set; }

    /// <summary>
    /// Format of the file
    /// https://dublincore.org/specifications/dublin-core/dcmi-terms/
    /// </summary>
    [XmlElement("format", Namespace = "http://purl.org/dc/terms/format")]
    public string Format { get; set; }

    [XmlElement("language", Namespace = "http://purl.org/dc/terms/")]
    public string Language { get; set; }

    [XmlElement("content")]
    public FeedEntryContent Content { get; set; }

    [XmlElement("link")]
    public List<FeedLink> Links = new List<FeedLink>();

    // [XmlElement("author")]
    // public List<FeedAuthor> Authors = new List<FeedAuthor>();

    // [XmlElement("category")]
    // public List<FeedCategory> Categories = new List<FeedCategory>();
}
