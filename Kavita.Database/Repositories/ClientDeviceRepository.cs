using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class ClientDeviceRepository(DataContext context, IMapper mapper): IClientDeviceRepository
{
    public Task<ClientDevice?> GetClientDeviceById(int id, int userId, CancellationToken cancellationToken = default)
    {
        return context.ClientDevice.FirstOrDefaultAsync(c => c.Id == id && c.AppUserId == userId, cancellationToken);
    }

    public async Task<ClientDevice?> GetClientDeviceByClientFingerprint(int userId, string uiFingerprint, CancellationToken cancellationToken)
    {
        return await context.ClientDevice
            .Include(d => d.History.OrderByDescending(h => h.CapturedAtUtc).Take(1))
            .FirstOrDefaultAsync(d =>
                d.AppUserId == userId &&
                d.UiFingerprint == uiFingerprint &&
                d.IsActive, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<ClientDevice>> GetUserDevicesAsync(int userId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        return await context.ClientDevice
            .Where(d => d.AppUserId == userId)
            .WhereIf(!includeInactive, d => d.IsActive)
            .OrderByDescending(d => d.LastSeenUtc)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<ClientDeviceDto>> GetUserDeviceDtosAsync(int userId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        return await context.ClientDevice
            .Where(d => d.AppUserId == userId)
            .WhereIf(!includeInactive, d => d.IsActive)
            .OrderByDescending(d => d.LastSeenUtc)
            .ProjectTo<ClientDeviceDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<ClientDeviceDto>> GetAllUserDeviceDtos(bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await context.ClientDevice
            .WhereIf(!includeInactive, d => d.IsActive)
            .OrderByDescending(d => d.LastSeenUtc)
            .ProjectTo<ClientDeviceDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken: cancellationToken);
    }
}
