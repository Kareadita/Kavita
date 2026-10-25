using System.ComponentModel.DataAnnotations;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Stats.V3;

/// <summary>
/// KavitaStats - Media errors grouped by reason
/// </summary>
public sealed record MediaErrorStatV3
{
    [EnumDataType(typeof(MediaErrorReason))]
    public MediaErrorReason Reason { get; set; }
    public int Count { get; set; }
    public int DismissedCount { get; set; }
}
