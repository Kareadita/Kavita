namespace API.DTOs.Statistics;

public sealed record ReadingPaceDto
{
    public int HoursRead { get; set; }
    public int PagesRead { get; set; }
    public int WordsRead { get; set; }
    public int BooksRead { get; set; }
    public int ComicsRead { get; set; }
    public int DaysInRange { get; set; }
}
