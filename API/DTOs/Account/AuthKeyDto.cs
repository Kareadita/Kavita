using System;
using API.Entities.Enums.User;

namespace API.DTOs.Account;

public sealed record AuthKeyDto
{
    public int Id { get; set; }
    /// <summary>
    /// Actual key
    /// </summary>
    /// <remarks>This is a variable string length from [6-32] alphanumeric characters</remarks>
    public required string Key { get; set; }
    /// <summary>
    /// Name of the key
    /// </summary>
    public required string Name { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    /// <summary>
    /// An Optional time which the Key expires
    /// </summary>
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastAccessedAtUtc { get; set; }

    /// <summary>
    /// Kavita will have a short-lived key
    /// </summary>
    public AuthKeyProvider Provider { get; set; } = AuthKeyProvider.User;
}
