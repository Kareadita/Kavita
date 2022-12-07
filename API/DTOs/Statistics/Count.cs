namespace API.DTOs.Statistics;

public class StatCount<T> : ICount<T>
{
    public T Value { get; set; }
    public int Count { get; set; }
}
