using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Device;
using API.Entities;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IDeviceService
{
    Task<Device> Create(CreateDeviceDto dto, AppUser userWithDevices);
    Task<Device> Update(UpdateDeviceDto dto, AppUser userWithDevices);
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

    public async Task<Device?> Create(CreateDeviceDto dto, AppUser userWithDevices)
    {
        try
        {
            var existingDevice = userWithDevices.Devices.SingleOrDefault(d => d.Name.Equals(dto.Name));
            if (existingDevice != null) throw new KavitaException("A device with this name already exists");

            existingDevice = DbFactory.Device(dto.Name);
            existingDevice.Platform = dto.Platform;
            existingDevice.EmailAddress = dto.EmailAddress;

            if (!_unitOfWork.HasChanges()) return existingDevice;
            if (await _unitOfWork.CommitAsync()) return existingDevice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error when creating your device");
            await _unitOfWork.RollbackAsync();
        }

        return null;
    }

    public async Task<Device?> Update(UpdateDeviceDto dto, AppUser userWithDevices)
    {
        try
        {
            var existingDevice = userWithDevices.Devices.SingleOrDefault(d => d.Id == dto.Id);
            if (existingDevice == null) throw new KavitaException("This device doesn't exist yet. Please create first");

            existingDevice.Name = dto.Name;
            existingDevice.Platform = dto.Platform;
            existingDevice.EmailAddress = dto.EmailAddress;

            if (!_unitOfWork.HasChanges()) return existingDevice;
            if (await _unitOfWork.CommitAsync()) return existingDevice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error when updating your device");
            await _unitOfWork.RollbackAsync();
        }

        return null;
    }
}
