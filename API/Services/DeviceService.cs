using System.Threading.Tasks;
using API.Data;
using API.Entities;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IDeviceService
{
    Task<Device> CreateDevice(string name);
}

public class DeviceService : IDeviceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(IUnitOfWork unitOfWork, ILogger<DeviceService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Device> CreateDevice(string name)
    {
        var existingDevice = await _unitOfWork.DeviceRepository.FindByNameAsync(name);

        if (existingDevice == null) existingDevice = DbFactory.Device(name);

        return existingDevice;
    }
}
