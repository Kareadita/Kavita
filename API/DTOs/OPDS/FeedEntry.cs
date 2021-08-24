using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace API.DTOs.OPDS
{
    public class FeedEntry
    {
        [XmlElement("updated")]
        public string Updated { get; init; } = DateTime.UtcNow.ToString("s");

        [XmlElement("id")]
        public string Id { get; set; }

        [XmlElement("title")]
        public string Title { get; set; }

        [XmlElement("language", Namespace = "http://purl.org/dc/terms/")]
        public string Language { get; set; }

        [XmlElement("content")]
         public FeedEntryContent Content { get; set; }

        // [XmlElement("series", Namespace = "urn:dotopds:v1.0")]
        // public FeedEntrySeries Series { get; set; }

        [XmlElement("link")]
        public List<FeedLink> Links = new List<FeedLink>();

        // [XmlElement("author")]
        // public List<FeedAuthor> Authors = new List<FeedAuthor>();

        // [XmlElement("category")]
        // public List<FeedCategory> Categories = new List<FeedCategory>();

    }
}
