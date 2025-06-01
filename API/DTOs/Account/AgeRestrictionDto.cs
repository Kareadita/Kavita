using API.Entities.Enums;

namespace API.DTOs.Account;

public sealed record AgeRestrictionDto
{
    /// <summary>
    /// The maximum age rating a user has access to. -1 if not applicable
    /// </summary>
    public required AgeRating AgeRating { get; init; } = AgeRating.NotApplicable;
    /// <summary>
    /// Are Unknowns explicitly allowed against age rating
    /// </summary>
    /// <remarks>Unknown is always lowest and default age rating. Setting this to false will ensure Teen age rating applies and unknowns are still filtered</remarks>
    public required bool IncludeUnknowns { get; init; } = false;
}
