using System;

namespace Kavita.Models.DTOs.Account;

public sealed record AuthKeyExpiresAtDto
{
    public required DateTime? ExpiresAt { get; set; }
}
