
using Kavita.Models.DTOs.Filtering.v2.FilterFields;

namespace Kavita.Models.DTOs.Filtering.v2;

public sealed record SeriesFilterStatementDto : IFilterStatement<SeriesFilterField>
{
    public FilterComparison Comparison { get; set; }
    public SeriesFilterField Field { get; set; }
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

public sealed record ReadingListFilterStatementDto : IFilterStatement<ReadingListFilterField>
{
    public FilterComparison Comparison { get; set; }
    public ReadingListFilterField Field { get; set; }
    public string Value { get; set; }
}
