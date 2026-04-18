using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;

namespace Kavita.Models.DTOs.Dashboard;

public sealed record SmartFilterDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>
    /// This is the Filter url encoded. It is decoded and reconstructed into a <see cref="SeriesFilterV2Dto"/>
    /// </summary>
    public required string Filter { get; set; }
    /// <summary>
    /// The underlying type which drives which API and entity to expect
    /// </summary>
    public required FilterEntityType EntityType { get; set; }
}
