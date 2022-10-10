using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;

namespace API.DTOs.Account;

public class UpdateAgeRestrictionDto
{
    [Required]
    public AgeRating AgeRestriction { get; set; }
}
