using System;

namespace API.DTOs.Progress;

public sealed record ClientDeviceDto
{
    public int Id { get; set; }
    /// <summary>
    /// User-friendly name, defaults to generated name like "Chrome on Windows"
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// Most recent stable ClientInfoData (excluding IP/timestamp changes)
    /// </summary>
    public ClientInfoDto CurrentClientInfo { get; set; } = new();

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    public string OwnerUsername { get; set; }
    public int OwnerUserId { get; set; }
}
