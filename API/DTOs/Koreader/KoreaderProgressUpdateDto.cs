using System;

namespace API.DTOs.Koreader;

public class KoreaderProgressUpdateDto
{
    /// <summary>
    /// This is the Koreader hash of the book. It is used to identify the book.
    /// </summary>
    public string Document { get; set; }
    /// <summary>
    /// UTC Timestamp to return to KOReader
    /// </summary>
    public DateTime Timestamp { get; set; }
}
