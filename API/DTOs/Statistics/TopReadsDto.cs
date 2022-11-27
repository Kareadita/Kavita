using System.Collections.Generic;

namespace API.DTOs.Statistics;

public class TopReadDto
{
    public string SeriesName { get; set; }
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    /// <summary>
    /// How many distinct user (id) read events
    /// </summary>
    public int UsersRead { get; set; }
}

public class TopReadsDto
{
    public IEnumerable<TopReadDto> Comics { get; set; }
    public IEnumerable<TopReadDto> Manga { get; set; }
    public IEnumerable<TopReadDto> Books { get; set; }
}
