namespace API.DTOs.Statistics;

public interface ICount<T>
{
    public T Value { get; set; }
    public int Count { get; set; }
}
