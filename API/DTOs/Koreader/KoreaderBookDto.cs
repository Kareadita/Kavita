using API.DTOs.Progress;

namespace API.DTOs.Koreader;

/// <summary>
/// This is the interface for receiving and sending updates to Koreader. The only fields
/// that are actually used are the Document and Progress fields.
/// </summary>
public class KoreaderBookDto
{
    /// <summary>
    /// This is the Koreader hash of the book. It is used to identify the book.
    /// </summary>
    public string Document { get; set; }
    /// <summary>
    /// A randomly generated id from the koreader device. Only used to maintain the Koreader interface.
    /// </summary>
    public string Device_id { get; set; }
    /// <summary>
    /// The Koreader device name. Only used to maintain the Koreader interface.
    /// </summary>
    public string Device { get; set; }
    /// <summary>
    /// Percent progress of the book. Only used to maintain the Koreader interface.
    /// </summary>
    public float Percentage { get; set; }
    /// <summary>
    /// An XPath string read by Koreader to determine the location within the epub.
    /// Essentially, it is Koreader's equivalent to ProgressDto.BookScrollId.
    /// </summary>
    /// <seealso cref="ProgressDto.BookScrollId"/>
    public string Progress { get; set; }
}
