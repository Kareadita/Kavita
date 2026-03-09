using System.Xml.Serialization;

namespace Kavita.Models.DTOs.OPDS;

public sealed record FeedEntryContent
{
    [XmlAttribute("type")]
    public string Type = "text";
    [XmlText]
    public string Text;
}
