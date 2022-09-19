using System.Threading.Tasks;
using API.Entities;
using AutoMapper;

namespace API.Data.Repositories;

public interface IDeviceRepository
{
    void Attach(Device device);
    void Remove(Device device);
    Task<Device> FindByNameAsync(string name);
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
}
