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

    [XmlElement(ElementName="Books")]
    public CblBooks Books { get; set; }
}
