using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Device;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

public interface IDeviceRepository
{
    void Update(Device device);
    Task<IEnumerable<DeviceDto>> GetDevicesForUserAsync(int userId);
    Task<Device?> GetDeviceById(int deviceId);
}

public class DeviceRepository : IDeviceRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public DeviceRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Update(Device device)
    {
        _context.Entry(device).State = EntityState.Modified;
    }

    public async Task<IEnumerable<DeviceDto>> GetDevicesForUserAsync(int userId)
    {
        return await _context.Device
            .Where(d => d.AppUserId == userId)
            .OrderBy(d => d.LastUsed)
            .ProjectTo<DeviceDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<Device?> GetDeviceById(int deviceId)
    {
        return await _context.Device
            .Where(d => d.Id == deviceId)
            .SingleOrDefaultAsync();
    }
}
