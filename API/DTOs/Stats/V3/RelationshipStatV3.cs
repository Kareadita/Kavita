using API.Entities.Enums;

namespace API.DTOs.Stats.V3;

/// <summary>
/// KavitaStats - Information about Series Relationships
/// </summary>
public sealed record RelationshipStatV3
{
    public int Count { get; set; }
    public RelationKind Relationship { get; set; }
}
