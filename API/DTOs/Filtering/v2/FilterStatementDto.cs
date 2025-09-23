
namespace API.DTOs.Filtering.v2;

public sealed record FilterStatementDto
{
    public FilterComparison Comparison { get; set; }
    public FilterField Field { get; set; }
    public string Value { get; set; }
}

public sealed record PersonFilterStatementDto
{
    public FilterComparison Comparison { get; set; }
    public PersonFilterField Field { get; set; }
    public string Value { get; set; }
}

public sealed record AnnotationFilterStatementDto
{
    public FilterComparison Comparison { get; set; }
    public AnnotationFilterField Field { get; set; }
    public string Value { get; set; }
}
