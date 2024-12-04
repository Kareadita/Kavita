using API.Entities.Enums;

namespace API.DTOs.Stats.V3;

/// <summary>
/// KavitaStats - Information about Series Relationships
/// </summary>
public class RelationshipStatV3
{
    public int Count { get; set; }
    public RelationKind Relationship { get; set; }
}
