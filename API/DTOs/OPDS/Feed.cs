using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace API.DTOs.OPDS
{
    /// <summary>
    ///
    /// </summary>
    [XmlRoot("feed", Namespace = "http://www.w3.org/2005/Atom")]
    public class Feed
    {
        [XmlElement("updated")]
        public string Updated { get; init; } = DateTime.UtcNow.ToString("s");

        [XmlElement("id")]
        public string Id { get; set; }

        [XmlElement("title")]
        public string Title { get; set; }

        [XmlElement("icon")]
        public string Icon { get; } = "/theme/favicon.ico";

        [XmlElement("author")]
        public Author Author { get; set; } = new Author()
        {
            Name = "Kavita",
            Uri = "https://kavitareader.com"
        };

        // [XmlElement("totalResults", Namespace = "http://a9.com/-/spec/opensearch/1.1/")]
        // public int? Total { get; set; }
        //
        // [XmlElement("itemsPerPage", Namespace = "http://a9.com/-/spec/opensearch/1.1/")]
        // public int ItemsPerPage { get; set; } = 20;

        [XmlElement("link")]
        public List<FeedLink> Links { get; set; } = new List<FeedLink>() ;

        [XmlElement("entry")]
        public List<FeedEntry> Entries { get; set; } = new List<FeedEntry>();
    }
}
