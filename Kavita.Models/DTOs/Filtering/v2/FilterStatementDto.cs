
namespace Kavita.Models.DTOs.Filtering.v2;

public sealed record FilterStatementDto : IFilterStatement<FilterField>
{
    public FilterComparison Comparison { get; set; }
    public FilterField Field { get; set; }
    public string Value { get; set; }
}

public sealed record PersonFilterStatementDto : IFilterStatement<PersonFilterField>
{
    public FilterComparison Comparison { get; set; }
    public PersonFilterField Field { get; set; }
    public string Value { get; set; }
}

public sealed record AnnotationFilterStatementDto : IFilterStatement<AnnotationFilterField>
{
    public FilterComparison Comparison { get; set; }
    public AnnotationFilterField Field { get; set; }
    public string Value { get; set; }
}
