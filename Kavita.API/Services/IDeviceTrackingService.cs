using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Progress;

namespace Kavita.API.Services;

public interface IDeviceTrackingService
{
    Task<int> TrackDeviceAsync(int userId, ClientInfoData clientInfo, string? uiFingerprint, CancellationToken ct);
    Task ClearDeviceCacheAsync(int deviceId);
    Task ClearUserDeviceCachesAsync(int userId);
}
