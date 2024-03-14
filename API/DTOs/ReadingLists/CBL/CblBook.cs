using System.Xml.Serialization;
using API.Data.Metadata;

namespace API.DTOs.ReadingLists.CBL;


[XmlRoot(ElementName="Book")]
public class CblBook
{
    [XmlAttribute("Series")]
    public string Series { get; set; }
    /// <summary>
    /// Chapter Number
    /// </summary>
    [XmlAttribute("Number")]
    public string Number { get; set; }
    /// <summary>
    /// Volume Number (usually for Comics they are the year)
    /// </summary>
    [XmlAttribute("Volume")]
    public string Volume { get; set; }
    [XmlAttribute("Year")]
    public string Year { get; set; }
    /// <summary>
    /// Main Series, Annual, Limited Series
    /// </summary>
    /// <remarks>This maps to <see cref="ComicInfo">Format</see> tag</remarks>
    [XmlAttribute("Format")]
    public string Format { get; set; }
    /// <summary>
    /// The underlying filetype
    /// </summary>
    /// <remarks>This is not part of the standard and explicitly for Kavita to support non cbz/cbr files</remarks>
    [XmlAttribute("FileType")]
    public string FileType { get; set; }
}
