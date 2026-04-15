using Kavita.Models.DTOs.Filtering.v2;

namespace Kavita.Models.Builders;
#nullable enable

// TODO: See if we actually need this or not
public class FilterV2DtoBuilder : IEntityBuilder<FilterV2Dto>
{
    private readonly FilterV2Dto _dto;
    public FilterV2Dto Build() => _dto;

    public FilterV2DtoBuilder(FilterV2Dto? dto)
    {
        _dto ??= dto ?? new FilterV2Dto();
    }

    public FilterV2DtoBuilder WithLibraries(params int[] libraries)
    {
        WithStatement(FilterComparison.MustContains, SeriesFilterField.Libraries,  string.Join(",", libraries));
        return this;
    }

    public FilterV2DtoBuilder WithStatement(FilterComparison comparison, SeriesFilterField field, string value)
    {
        _dto.Statements.Add(new FilterStatementDto()
        {
            Comparison = comparison,
            Field = field,
            Value = value
        });
        return this;
    }
}
