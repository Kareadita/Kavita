using System.Threading;
using Kavita.API.Services;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;

namespace Kavita.Server.Middleware;

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
