using System;
using System.ComponentModel.DataAnnotations;
using API.Helpers;

namespace API.DTOs.Account;
#nullable enable

public sealed record RotateAuthKeyRequestDto
{
    [Required]
    public int KeyLength { get; set; }

    public required string Name { get; set; }
    public string? ExpiresUtc { get; set; }
}
