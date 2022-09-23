﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Device;
using API.DTOs.Email;
using API.Entities;
using API.Entities.Enums;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface IDeviceService
{
    Task<Device> Create(CreateDeviceDto dto, AppUser userWithDevices);
    Task<Device> Update(UpdateDeviceDto dto, AppUser userWithDevices);
    Task<bool> Delete(AppUser userWithDevices, int deviceId);
    Task<bool> SendTo(int chapterId, int deviceId);
}

public class DeviceService : IDeviceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeviceService> _logger;
    private readonly IEmailService _emailService;

    /// <summary>
    /// Size Limit, 25 MB
    /// </summary>
    private const int SizeLimit = 26_214_400;

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
            var existingDevice = userWithDevices.Devices.SingleOrDefault(d => d.Name.Equals(dto.Name));
            if (existingDevice != null) throw new KavitaException("A device with this name already exists");

            existingDevice = DbFactory.Device(dto.Name);
            existingDevice.Platform = dto.Platform;
            existingDevice.EmailAddress = dto.EmailAddress;


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

    public async Task<bool> SendTo(int chapterId, int deviceId)
    {
        var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
        if (files.Any(f => f.Format is not (MangaFormat.Epub or MangaFormat.Pdf)))
            throw new KavitaException("Cannot Send non Epub or Pdf to devices as not supported");

        var device = await _unitOfWork.DeviceRepository.GetDeviceDtoById(deviceId);
        var success = await _emailService.SendFilesToEmail(new SendToDto()
        {
            DestinationEmail = device.EmailAddress,
            FilePaths = files.Select(m => m.FilePath)
        });
        return success;
    }
}
