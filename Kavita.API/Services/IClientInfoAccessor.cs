using Kavita.Models.Entities.Progress;

namespace Kavita.API.Services;

/// <summary>
/// Provides access to client information for the current request.
/// This service captures details about the client making the request including
/// browser info, device type, authentication method, etc.
/// </summary>
public interface IClientInfoAccessor
{
    /// <summary>
    /// Gets the client information for the current request.
    /// Returns null if called outside an HTTP request context (e.g., background jobs).
    /// </summary>
    ClientInfoData? Current { get; }
    string? CurrentUiFingerprint { get; }
    /// <summary>
    /// Client Device PK
    /// </summary>
    int? CurrentDeviceId { get; }
}
