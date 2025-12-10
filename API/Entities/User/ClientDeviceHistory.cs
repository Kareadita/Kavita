using System;
using API.Entities.Progress;
using API.Entities.User;

namespace API.Entities;

public class ClientDeviceHistory
{
    public int Id { get; set; }
    public int DeviceId { get; set; }

    /// <summary>
    /// Snapshot of ClientInfoData at a point in time
    /// Useful for tracking IP changes, browser updates, etc.
    /// </summary>
    public ClientInfoData ClientInfo { get; set; } = new();

    public DateTime CapturedAtUtc { get; set; }

    // Navigation
    public ClientDevice Device { get; set; } = null!;
}
