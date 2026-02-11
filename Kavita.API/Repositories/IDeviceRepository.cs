using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Device.EmailDevice;
using Kavita.Models.Entities;

namespace Kavita.API.Repositories;

public interface IDeviceRepository
{
    void Update(Device device);
    Task<IList<EmailDeviceDto>> GetDevicesForUserAsync(int userId, CancellationToken ct = default);
    Task<Device?> GetDeviceById(int deviceId, CancellationToken ct = default);
}
