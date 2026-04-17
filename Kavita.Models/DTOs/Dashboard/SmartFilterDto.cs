using Kavita.Models.DTOs.Filtering.v2;

namespace Kavita.Models.DTOs.Dashboard;

public sealed record SmartFilterDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>
    /// This is the Filter url encoded. It is decoded and reconstructed into a <see cref="SeriesFilterV2Dto"/>
    /// </summary>
    public required string Filter { get; set; }
}
