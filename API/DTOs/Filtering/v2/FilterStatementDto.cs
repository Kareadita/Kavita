namespace API.DTOs.Filtering.v2;

public sealed record FilterStatementDto
{
    public FilterComparison Comparison { get; set; }
    public FilterField Field { get; set; }
    public string Value { get; set; }
}
