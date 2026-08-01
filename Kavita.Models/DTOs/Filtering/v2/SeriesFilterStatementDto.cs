
using Kavita.Models.DTOs.Filtering.v2.FilterFields;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Filtering.v2;

public sealed record SeriesFilterStatementDto : IFilterStatement<SeriesFilterField>
{
    [EnumDataType(typeof(FilterComparison))]
    public FilterComparison Comparison { get; set; }
    [EnumDataType(typeof(SeriesFilterField))]
    public SeriesFilterField Field { get; set; }
    public string Value { get; set; }
}

public sealed record PersonFilterStatementDto : IFilterStatement<PersonFilterField>
{
    [EnumDataType(typeof(FilterComparison))]
    public FilterComparison Comparison { get; set; }
    [EnumDataType(typeof(PersonFilterField))]
    public PersonFilterField Field { get; set; }
    public string Value { get; set; }
}

public sealed record AnnotationFilterStatementDto : IFilterStatement<AnnotationFilterField>
{
    [EnumDataType(typeof(FilterComparison))]
    public FilterComparison Comparison { get; set; }
    [EnumDataType(typeof(AnnotationFilterField))]
    public AnnotationFilterField Field { get; set; }
    public string Value { get; set; }
}

public sealed record ReadingListFilterStatementDto : IFilterStatement<ReadingListFilterField>
{
    [EnumDataType(typeof(FilterComparison))]
    public FilterComparison Comparison { get; set; }
    [EnumDataType(typeof(ReadingListFilterField))]
    public ReadingListFilterField Field { get; set; }
    public string Value { get; set; }
}