using System;

namespace API.DTOs.Scrobbling;

public class ScrobbleErrorDto
{
    /// <summary>
    /// Developer defined string
    /// </summary>
    public string Comment { get; set; }
    /// <summary>
    /// List of providers that could not
    /// </summary>
    public string Details { get; set; }
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public DateTime Created { get; set; }
}
