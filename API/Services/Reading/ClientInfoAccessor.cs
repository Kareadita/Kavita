using System.Threading;
using API.Entities.Progress;
using API.Entities.User;

namespace API.Services.Reading;
#nullable enable

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

/// <summary>
/// Thread-safe accessor for client information using AsyncLocal storage.
/// Client info is set by middleware at the start of each request and automatically
/// cleared when the request completes.
/// </summary>
public class ClientInfoAccessor : IClientInfoAccessor
{
    private static readonly AsyncLocal<ClientInfoData?> ClientInfo = new();
    private static readonly AsyncLocal<string?> UiFingerprint = new();
    private static readonly AsyncLocal<int?> DeviceId = new();

    public ClientInfoData? Current => ClientInfo.Value;
    public string? CurrentUiFingerprint => UiFingerprint.Value;
    public int? CurrentDeviceId => DeviceId.Value;

    /// <summary>
    /// Sets the client info for the current async context.
    /// Should only be called by middleware.
    /// </summary>
    internal static void SetClientInfo(ClientInfoData? info)
    {
        ClientInfo.Value = info;
    }

    /// <summary>
    /// Sets the client fingerprint for the current async context.
    /// Should only be called by middleware.
    /// </summary>
    internal static void SetUiFingerprint(string uiFingerprint)
    {
        UiFingerprint.Value = uiFingerprint;
    }

    /// <summary>
    /// Sets the <see cref="ClientDevice.Id"/> for the current async context.
    /// Should only be called by middleware.
    /// </summary>
    internal static void SetDeviceId(int clientDeviceId)
    {
        DeviceId.Value = clientDeviceId;
    }
}
