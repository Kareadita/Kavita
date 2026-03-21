using System.Collections.Generic;
using System.Xml.Serialization;

namespace Kavita.Models.DTOs.ReadingLists.CBL.V1;

/// <summary>
/// External database reference embedded in a V1 CBL Book entry.
/// Maps a provider name to its series and issue identifiers
/// </summary>
[XmlRoot(ElementName="Database")]
public sealed record CblBookDatabase
{
    /// <summary>
    /// Provider short-name (e.g. "cv" for ComicVine, "metron", "gcd")
    /// </summary>
    [XmlAttribute("Name")]
    public string Name { get; set; }
    /// <summary>
    /// The provider's unique identifier for the series
    /// </summary>
    [XmlAttribute("Series")]
    public string Series { get; set; }
    /// <summary>
    /// The provider's unique identifier for the issue
    /// </summary>
    [XmlAttribute("Issue")]
    public string Issue { get; set; }
}

/// <summary>
/// A single book (issue) entry in a V1 XML CBL reading list
/// </summary>
[XmlRoot(ElementName="Book")]
public sealed record CblBook
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
    /// <remarks>This maps to <c>ComicInfo.Format</c> tag</remarks>
    [XmlAttribute("Format")]
    public string Format { get; set; }
    /// <summary>
    /// The underlying filetype
    /// </summary>
    /// <remarks>This is not part of the standard and explicitly for Kavita to support non cbz/cbr files</remarks>
    [XmlAttribute("FileType")]
    public string FileType { get; set; }
    /// <summary>
    /// External database references (e.g. ComicVine, Metron)
    /// </summary>
    [XmlElement("Database")]
    public List<CblBookDatabase> Databases { get; set; } = [];
}
