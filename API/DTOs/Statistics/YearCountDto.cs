namespace API.DTOs.Statistics;

public class YearCountDto : ICount<int>
{
    /// <summary>
    /// Release Year
    /// </summary>
    public int Value { get; set; }
    public int Count { get; set; }
}
