namespace API.DTOs.Statistics;

public sealed record ProfileStatBarDto
{
    public int BooksRead { get; set; }
    public int ComicsRead { get; set; }
    public int PagesRead { get; set; }
    public int WordsRead { get; set; }
    public int AuthorsRead { get; set; }
    public int Reviews { get; set; }
    public int Ratings { get; set; }
}
