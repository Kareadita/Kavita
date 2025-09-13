namespace API.DTOs.Statistics;

public sealed record StatCount<T> : ICount<T>
{
    public T Value { get; set; } = default!;
    public long Count { get; set; }
}
