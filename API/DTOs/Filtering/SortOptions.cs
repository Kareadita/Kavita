namespace API.DTOs.Filtering;

/// <summary>
/// Sorting Options for a query
/// </summary>
public sealed record SortOptions
{
    public SortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}

/// <summary>
/// All Sorting Options for a query related to Person Entity
/// </summary>
public sealed record PersonSortOptions
{
    public PersonSortField SortField { get; set; }
    public bool IsAscending { get; set; } = true;
}
