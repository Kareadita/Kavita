using System;
using System.Collections.Generic;
using API.Constants;
using API.Entities.Progress;

namespace API.Entities.User;
#nullable enable

public class ClientDevice
{
    public int Id { get; set; }
    /// <summary>
    /// Client-provided device identifier (from <see cref="Headers.ClientDeviceFingerprint"/> header)
    /// Null for clients that don't send it (OPDS readers, etc.)
    /// </summary>
    public string? UiFingerprint { get; set; }

    /// <summary>
    /// Server-computed fingerprint for device matching
    /// Hash of: ClientType + Platform + DeviceType + (Browser+BrowserVersion for web-app)
    /// </summary>
    public string DeviceFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly name, defaults to generated name like "Chrome on Windows"
    /// </summary>
    /// <remarks>Generated on first seen, user can customize after the fact</remarks>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// Most recent stable ClientInfoData (excluding IP/timestamp changes)
    /// </summary>
    public ClientInfoData CurrentClientInfo { get; set; } = new();

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    /// <summary>
    /// Soft delete - removed devices stay in DB for history
    /// </summary>
    public bool IsActive { get; set; } = true;

    // TODO: Put an optional string? for AppUserAuthKey used (it might change, so it should be last seen)

    // Navigation properties
    public int AppUserId { get; set; }
    public virtual AppUser AppUser { get; set; } = null!;
    public ICollection<ClientDeviceHistory> History { get; set; } = [];
}
