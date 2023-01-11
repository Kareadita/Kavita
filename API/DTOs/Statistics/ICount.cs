namespace API.DTOs.Statistics;

public interface ICount<T>
{
    public T Value { get; set; }
    public long Count { get; set; }
}
