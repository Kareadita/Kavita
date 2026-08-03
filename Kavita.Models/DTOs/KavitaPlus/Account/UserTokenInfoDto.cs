using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus.Account;

/// <summary>
/// Represents information around a user's tokens and their status
/// </summary>
public sealed record UserTokenInfoDto
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public List<TokenValidityInfoDto> Tokens { get; set; }
}

public sealed record TokenValidityInfoDto
{
    [EnumDataType(typeof(ScrobbleProvider))]
    public ScrobbleProvider Provider { get; set; }
    public DateTime ValidUntilUtc { get; set; }
}