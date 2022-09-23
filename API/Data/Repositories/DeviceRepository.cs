using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Device;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IDeviceRepository
{
    void Attach(Device device);
    void Remove(Device device);
    Task<Device> FindByNameAsync(string name);
    Task<IEnumerable<DeviceDto>> GetDevicesForUserAsync(int userId);
    Task<DeviceDto> GetDeviceDtoById(int deviceId);
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

    public void Attach(Device device)
    {
        throw new System.NotImplementedException();
    }

    public void Remove(Device device)
    {
        throw new System.NotImplementedException();
    }

    public Task<Device?> FindByNameAsync(string name)
    {
        return null;
    }

    public async Task<IEnumerable<DeviceDto>> GetDevicesForUserAsync(int userId)
    {
        return await _context.Device
            .Where(d => d.AppUserId == userId)
            .ProjectTo<DeviceDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<DeviceDto> GetDeviceDtoById(int deviceId)
    {
        return await _context.Device
            .Where(d => d.Id == deviceId)
            .ProjectTo<DeviceDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }
}
