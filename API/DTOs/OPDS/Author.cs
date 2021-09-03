﻿using System.Xml.Serialization;

namespace API.DTOs.OPDS
{
    public class Author
    {
        [XmlElement("name")]
        public string Name { get; set; }
        [XmlElement("uri")]
        public string Uri { get; set; }
    }
}
