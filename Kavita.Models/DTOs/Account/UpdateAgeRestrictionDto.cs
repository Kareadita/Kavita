using System.ComponentModel.DataAnnotations;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Account;

public sealed record UpdateAgeRestrictionDto
{
    [EnumDataType(typeof(AgeRating))]
    [Required]
    public AgeRating AgeRating { get; set; }
    [Required]
    public bool IncludeUnknowns { get; set; }
}
