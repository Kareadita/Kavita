using System;

namespace API.DTOs.Scrobbling;

public class ScrobbleHoldDto
{
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
}
