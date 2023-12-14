using System;
using System.Collections.Generic;
using API.DTOs.Account;

namespace API.DTOs;
#nullable enable

/// <summary>
/// Represents a member of a Kavita server.
/// </summary>
public class MemberDto
{
    public int Id { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    /// <summary>
    /// If the member is still pending or not
    /// </summary>
    public bool IsPending { get; init; }
    public AgeRestrictionDto? AgeRestriction { get; init; }
    public DateTime Created { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime LastActive { get; init; }
    public DateTime LastActiveUtc { get; init; }
    public IEnumerable<LibraryDto>? Libraries { get; init; }
    public IEnumerable<string>? Roles { get; init; }
}
