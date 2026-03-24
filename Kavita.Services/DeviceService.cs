using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.Common;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Device.EmailDevice;
using Kavita.Models.DTOs.Email;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Device;
using Kavita.Models.Entities.User;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class DeviceService(
    IUnitOfWork unitOfWork,
    ILogger<DeviceService> logger,
    IEmailService emailService,
    IReadingProfileService readingProfileService)
    : IDeviceService
{
    public async Task<Device?> Create(CreateEmailDeviceDto dto, AppUser userWithDevices, CancellationToken ct = default)
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
            unitOfWork.UserRepository.Update(userWithDevices);

            if (!unitOfWork.HasChanges()) return existingDevice;
            if (await unitOfWork.CommitAsync(ct)) return existingDevice;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an error when creating your device");
            await unitOfWork.RollbackAsync(ct);
        }

        return null;
    }

    public async Task<Device?> Update(UpdateEmailDeviceDto dto, AppUser userWithDevices, CancellationToken ct = default)
    {
        try
        {
            var existingDevice = userWithDevices.Devices.SingleOrDefault(d => d.Id == dto.Id);
            if (existingDevice == null) throw new KavitaException("device-not-created");

            existingDevice.Name = dto.Name;
            existingDevice.Platform = dto.Platform;
            existingDevice.EmailAddress = dto.EmailAddress;

            if (!unitOfWork.HasChanges()) return existingDevice;
            if (await unitOfWork.CommitAsync(ct)) return existingDevice;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an error when updating your device");
            await unitOfWork.RollbackAsync(ct);
        }

        return null;
    }

    public async Task<bool> Delete(AppUser userWithDevices, int deviceId, CancellationToken ct = default)
    {
        try
        {
            userWithDevices.Devices = userWithDevices.Devices.Where(d => d.Id != deviceId).ToList();
            unitOfWork.UserRepository.Update(userWithDevices);

            await readingProfileService.RemoveDeviceLinks(userWithDevices.Id, deviceId);

            if (!unitOfWork.HasChanges()) return true;
            if (await unitOfWork.CommitAsync(ct)) return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue with deleting the device, {DeviceId} for user {UserName}", deviceId, userWithDevices.UserName);
        }

        return false;
    }

    public async Task<bool> SendTo(IReadOnlyList<int> chapterIds, int deviceId, CancellationToken ct = default)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        if (!settings.IsEmailSetupForSendToDevice())
            throw new KavitaException("send-to-kavita-email");

        var device = await unitOfWork.DeviceRepository.GetDeviceById(deviceId, ct);
        if (device == null) throw new KavitaException("device-doesnt-exist");

        var files = await unitOfWork.ChapterRepository.GetFilesForChaptersAsync(chapterIds, ct);
        if (files.Any(f => f.Format is not (MangaFormat.Epub or MangaFormat.Pdf)) && device.Platform == EmailDevicePlatform.Kindle)
            throw new KavitaException("send-to-permission");

        // If the size of the files is too big
        if (files.Sum(f => f.Bytes) >= settings.SmtpConfig.SizeLimit)
            throw new KavitaException("send-to-size-limit");


        try
        {
            device.UpdateLastUsed();
            unitOfWork.DeviceRepository.Update(device);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an issue updating device last used time");
        }

        var success = await emailService.SendFilesToEmail(new SendToDto()
        {
            UserId = device.AppUserId,
            DestinationEmail = device.EmailAddress!,
            FilePaths = files.Select(m => m.FilePath)
        });

        return success;
    }
}
