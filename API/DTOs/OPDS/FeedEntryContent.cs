using System.Xml.Serialization;

namespace API.DTOs.OPDS;

public class FeedEntryContent
{
    [XmlAttribute("type")]
    public string Type = "text";
    [XmlText]
    public string Text;
}
