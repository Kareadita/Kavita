using System;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs;

/// <summary>
/// Represents a member of a Kavita server.
/// </summary>
public class MemberDto
{
    public int Id { get; init; }
    public string Username { get; init; }
    public string Email { get; init; }

    /// <summary>
    /// The maximum age rating a user has access to. -1 if not applicable
    /// </summary>
    public AgeRating AgeRestriction { get; init; } = AgeRating.NotApplicable;
    public DateTime Created { get; init; }
    public DateTime LastActive { get; init; }
    public IEnumerable<LibraryDto> Libraries { get; init; }
    public IEnumerable<string> Roles { get; init; }
}
