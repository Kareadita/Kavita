namespace API.DTOs.Filtering;
/// <summary>
/// Represents a range between two int/float/double
/// </summary>
public class Range<T>
{
    public T? Min { get; set; }
    public T? Max { get; set; }

    public override string ToString()
    {
        return $"{Min}-{Max}";
    }
}
