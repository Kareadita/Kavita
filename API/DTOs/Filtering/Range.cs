namespace API.DTOs.Filtering;
#nullable enable

/// <summary>
/// Represents a range between two int/float/double
/// </summary>
public class Range<T>
{
    public T? Min { get; init; }
    public T? Max { get; init; }

    public override string ToString()
    {
        return $"{Min}-{Max}";
    }
}
