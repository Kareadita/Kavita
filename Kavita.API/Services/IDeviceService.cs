using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Device.EmailDevice;
using Kavita.Models.Entities;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface IDeviceService
{
    Task<Device?> Create(CreateEmailDeviceDto dto, AppUser userWithDevices);
    Task<Device?> Update(UpdateEmailDeviceDto dto, AppUser userWithDevices);
    Task<bool> Delete(AppUser userWithDevices, int deviceId);
    Task<bool> SendTo(IReadOnlyList<int> chapterIds, int deviceId);
}
