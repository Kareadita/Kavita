using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Models.DTOs.Device.EmailDevice;
using Kavita.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class DeviceRepository(DataContext context, IMapper mapper) : IDeviceRepository
{
    public void Update(Device device)
    {
        context.Entry(device).State = EntityState.Modified;
    }

    public async Task<IList<EmailDeviceDto>> GetDevicesForUserAsync(int userId, CancellationToken ct = default)
    {
        return await context.Device
            .Where(d => d.AppUserId == userId)
            .OrderBy(d => d.LastUsed)
            .ProjectTo<EmailDeviceDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<Device?> GetDeviceById(int deviceId, CancellationToken ct = default)
    {
        return await context.Device
            .Where(d => d.Id == deviceId)
            .SingleOrDefaultAsync(ct);
    }
}
