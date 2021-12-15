namespace API.DTOs.Filtering;

/// <summary>
/// Sorting Options for a query
/// </summary>
public abstract class SortOptions
{
    public SortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}
