using System;
using System.Collections.Generic;
using API.Data.Misc;
using API.DTOs.Account;
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
    public AgeRestrictionDto AgeRestriction { get; init; }
    public DateTime Created { get; init; }
    public DateTime LastActive { get; init; }
    public IEnumerable<LibraryDto> Libraries { get; init; }
    public IEnumerable<string> Roles { get; init; }
}
