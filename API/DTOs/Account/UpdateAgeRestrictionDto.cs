using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;

namespace API.DTOs.Account;

public class UpdateAgeRestrictionDto
{
    [Required]
    public AgeRating AgeRating { get; set; }
    [Required]
    public bool IncludeUnknowns { get; set; }
}
