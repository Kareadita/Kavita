namespace API.DTOs.Filtering;

/// <summary>
/// Sorting Options for a query
/// </summary>
public sealed record SortOptions
{
    public SortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}
