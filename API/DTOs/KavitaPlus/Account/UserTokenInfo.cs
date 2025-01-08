using System;

namespace API.DTOs.KavitaPlus.Account;

/// <summary>
/// Represents information around a user's tokens and their status
/// </summary>
public class UserTokenInfo
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public bool IsAniListTokenSet { get; set; }
    public bool IsAniListTokenValid { get; set; }
    public DateTime AniListValidUntilUtc { get; set; }
    public bool IsMalTokenSet { get; set; }
}
