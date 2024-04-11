using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Device;
using API.DTOs.Email;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.Device;
using API.Helpers.Builders;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;
#nullable enable

public interface IDeviceService
{
    Task<Device?> Create(CreateDeviceDto dto, AppUser userWithDevices);
    Task<Device?> Update(UpdateDeviceDto dto, AppUser userWithDevices);
    Task<bool> Delete(AppUser userWithDevices, int deviceId);
    Task<bool> SendTo(IReadOnlyList<int> chapterIds, int deviceId);
}

public class DeviceService : IDeviceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeviceService> _logger;
    private readonly IEmailService _emailService;

    public DeviceService(IUnitOfWork unitOfWork, ILogger<DeviceService> logger, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<Device?> Create(CreateDeviceDto dto, AppUser userWithDevices)
    {
        try
        {
            userWithDevices.Devices ??= new List<Device>();
            var existingDevice = userWithDevices.Devices.SingleOrDefault(d => d.Name!.Equals(dto.Name));
            if (existingDevice != null) throw new KavitaException("device-duplicate");

            existingDevice = new DeviceBuilder(dto.Name)
                .WithPlatform(dto.Platform)
                .WithEmail(dto.EmailAddress)
                .Build();


            userWithDevices.Devices.Add(existingDevice);
            _unitOfWork.UserRepository.Update(userWithDevices);

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
            if (existingDevice == null) throw new KavitaException("device-not-created");

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

    public async Task<bool> Delete(AppUser userWithDevices, int deviceId)
    {
        try
        {
            userWithDevices.Devices = userWithDevices.Devices.Where(d => d.Id != deviceId).ToList();
            _unitOfWork.UserRepository.Update(userWithDevices);
            if (!_unitOfWork.HasChanges()) return true;
            if (await _unitOfWork.CommitAsync()) return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue with deleting the device, {DeviceId} for user {UserName}", deviceId, userWithDevices.UserName);
        }

        return false;
    }

    public async Task<bool> SendTo(IReadOnlyList<int> chapterIds, int deviceId)
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (!settings.IsEmailSetupForSendToDevice())
            throw new KavitaException("send-to-kavita-email");

        var device = await _unitOfWork.DeviceRepository.GetDeviceById(deviceId);
        if (device == null) throw new KavitaException("device-doesnt-exist");

        var files = await _unitOfWork.ChapterRepository.GetFilesForChaptersAsync(chapterIds);
        if (files.Any(f => f.Format is not (MangaFormat.Epub or MangaFormat.Pdf)) && device.Platform == DevicePlatform.Kindle)
            throw new KavitaException("send-to-permission");

        // If the size of the files is too big
        if (files.Sum(f => f.Bytes) >= settings.SmtpConfig.SizeLimit)
            throw new KavitaException("send-to-size-limit");


        try
        {
            device.UpdateLastUsed();
            _unitOfWork.DeviceRepository.Update(device);
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue updating device last used time");
        }

        var success = await _emailService.SendFilesToEmail(new SendToDto()
        {
            DestinationEmail = device.EmailAddress!,
            FilePaths = files.Select(m => m.FilePath)
        });

        return success;
    }
}
