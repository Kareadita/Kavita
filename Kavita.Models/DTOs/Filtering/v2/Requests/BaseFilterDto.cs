namespace Kavita.Models.DTOs.Filtering.v2.Requests;

/// <summary>
/// Sentinel used for generic binding at API layer
/// </summary>
public sealed class BaseFilterDto : IFilterDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public FilterCombination Combination { get; set; }
    public int LimitTo { get; set; }
    public FilterEntityType EntityType { get; init; }
}
