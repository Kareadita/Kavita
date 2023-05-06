using System.Collections.Generic;
using System.Xml.Serialization;

namespace API.DTOs.ReadingLists.CBL;


[XmlRoot(ElementName="Books")]
public class CblBooks
{
    [XmlElement(ElementName="Book")]
    public List<CblBook> Book { get; set; }
}


[XmlRoot(ElementName="ReadingList")]
public class CblReadingList
{
    /// <summary>
    /// Name of the Reading List
    /// </summary>
    [XmlElement(ElementName="Name")]
    public string Name { get; set; }

    /// <summary>
    /// Summary of the Reading List
    /// </summary>
    /// <remarks>This is not a standard, adding based on discussion with CBL Maintainers</remarks>
    [XmlElement(ElementName="Summary")]
    public string Summary { get; set; }

    /// <summary>
    /// Start Year of the Reading List. Overrides calculation
    /// </summary>
    /// <remarks>This is not a standard, adding based on discussion with CBL Maintainers</remarks>
    [XmlElement(ElementName="StartYear")]
    public int StartYear { get; set; } = -1;

    /// <summary>
    /// Start Year of the Reading List. Overrides calculation
    /// </summary>
    /// <remarks>This is not a standard, adding based on discussion with CBL Maintainers</remarks>
    [XmlElement(ElementName = "StartMonth")]
    public int StartMonth { get; set; } = -1;

    /// <summary>
    /// End Year of the Reading List. Overrides calculation
    /// </summary>
    /// <remarks>This is not a standard, adding based on discussion with CBL Maintainers</remarks>
    [XmlElement(ElementName="EndYear")]
    public int EndYear { get; set; } = -1;

    /// <summary>
    /// End Year of the Reading List. Overrides calculation
    /// </summary>
    /// <remarks>This is not a standard, adding based on discussion with CBL Maintainers</remarks>
    [XmlElement(ElementName="EndMonth")]
    public int EndMonth { get; set; } = -1;

    /// <summary>
    /// Issues of the Reading List
    /// </summary>
    [XmlElement(ElementName="Books")]
    public CblBooks Books { get; set; }
}
