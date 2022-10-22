using System.Xml.Serialization;

namespace API.DTOs.OPDS;

[XmlRoot("OpenSearchDescription", Namespace = "http://a9.com/-/spec/opensearch/1.1/")]
public class OpenSearchDescription
{
    /// <summary>
    /// Contains a brief human-readable title that identifies this search engine.
    /// </summary>
    public string ShortName { get; set; }
    /// <summary>
    /// Contains an extended human-readable title that identifies this search engine.
    /// </summary>
    public string LongName { get; set; }
    /// <summary>
    /// Contains a human-readable text description of the search engine.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// https://github.com/dewitt/opensearch/blob/master/opensearch-1-1-draft-6.md#the-url-element
    /// </summary>
    public SearchLink Url { get; set; }
    /// <summary>
    /// Contains a set of words that are used as keywords to identify and categorize this search content.
    /// Tags must be a single word and are delimited by the space character (' ').
    /// </summary>
    public string Tags { get; set; }
    /// <summary>
    /// Contains a URL that identifies the location of an image that can be used in association with this search content.
    /// <example><Image height="64" width="64" type="image/png">http://example.com/websearch.png</Image></example>
    /// </summary>
    public string Image { get; set; }
    public string InputEncoding { get; set; } = "UTF-8";
    public string OutputEncoding { get; set; } = "UTF-8";
    /// <summary>
    /// Contains the human-readable name or identifier of the creator or maintainer of the description document.
    /// </summary>
    public string Developer { get; set; } = "kavitareader.com";

}
