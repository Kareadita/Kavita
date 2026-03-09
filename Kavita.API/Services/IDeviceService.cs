using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Device.EmailDevice;
using Kavita.Models.Entities;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface IDeviceService
{
    Task<Device?> Create(CreateEmailDeviceDto dto, AppUser userWithDevices, CancellationToken ct = default);
    Task<Device?> Update(UpdateEmailDeviceDto dto, AppUser userWithDevices, CancellationToken ct = default);
    Task<bool> Delete(AppUser userWithDevices, int deviceId, CancellationToken ct = default);
    Task<bool> SendTo(IReadOnlyList<int> chapterIds, int deviceId, CancellationToken ct = default);
}
