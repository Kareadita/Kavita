using System.Collections.Generic;

namespace API.DTOs.Statistics;

public sealed record MostReadAuthorsDto
{

    public int AuthorId { get; init; }
    public string AuthorName { get; init; }
    public int TotalChaptersRead { get; init; }

    public IList<AuthorChapterDto> Chapters { get; init; }

}

public sealed record AuthorChapterDto
{
    public int LibraryId { get; set; }
    public int SeriesId { get; set; }
    public int ChapterId { get; set; }

    public string Title { get; set; }
}
