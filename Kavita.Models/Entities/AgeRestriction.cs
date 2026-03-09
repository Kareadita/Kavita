using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Entities;

public class AgeRestriction
{
    public AgeRating AgeRating { get; set; }
    public bool IncludeUnknowns { get; set; }
}
