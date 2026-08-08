using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Stats.V3;

/// <summary>
/// KavitaStats - Information about Series Relationships
/// </summary>
public sealed record RelationshipStatV3
{
    public int Count { get; set; }
    [EnumDataType(typeof(RelationKind))]
    public RelationKind Relationship { get; set; }
}