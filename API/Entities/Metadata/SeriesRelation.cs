using API.Entities.Enums;

namespace API.Entities.Metadata;

/// <summary>
/// A relation flows between one series and another.
/// Series ---kind---> target
/// </summary>
public sealed class SeriesRelation
{
    public int Id { get; set; }
    public RelationKind RelationKind { get; set; }

    public Series TargetSeries { get; set; } = null!;
    /// <summary>
    /// A is Sequel to B. In this example, TargetSeries is A. B will hold the foreign key.
    /// </summary>
    public int TargetSeriesId { get; set; }

    // Relationships
    public Series Series { get; set; } = null!;
    public int SeriesId { get; set; }
}
