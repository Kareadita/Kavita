using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Device.ClientDevice;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface IClientDeviceService
{
    Task<ClientDevice> IdentifyOrRegisterDeviceAsync(int userId, ClientInfoData clientInfo, string? uiFingerprint, CancellationToken cancellationToken = default);
    Task<bool> RenameDeviceAsync(int userId, int deviceId, string newName);
    Task<bool> DeleteDeviceAsync(int userId, int deviceId);
    Task UpdateFriendlyNameAsync(int userId, UpdateClientDeviceNameDto dto);
}
