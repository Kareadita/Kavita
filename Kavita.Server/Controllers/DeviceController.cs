using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Device.ClientDevice;
using Kavita.Models.DTOs.Device.EmailDevice;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.DTOs.SignalR;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// Responsible for interacting and creating Devices
/// </summary>
public class DeviceController(
    IUnitOfWork unitOfWork,
    IDeviceService deviceService,
    IEventHub eventHub,
    ILocalizationService localizationService,
    IMapper mapper,
    IClientDeviceService clientDeviceService)
    : BaseApiController
{
    /// <summary>
    /// Creates a new Device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<EmailDeviceDto>> CreateOrUpdateDevice(CreateEmailDeviceDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        try
        {
            var device = await deviceService.Create(dto, user);
            if (device == null)
                return BadRequest(await localizationService.TranslateAsync(UserId, "generic-device-create"));

            return Ok(mapper.Map<EmailDeviceDto>(device));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, ex.Message));
        }
    }

    /// <summary>
    /// Updates an existing Device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<EmailDeviceDto>> UpdateDevice(UpdateEmailDeviceDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Devices);
        if (user == null) return Unauthorized();

        var device = await deviceService.Update(dto, user);
        if (device == null) return BadRequest(await localizationService.TranslateAsync(UserId, "generic-device-update"));

        return Ok(mapper.Map<EmailDeviceDto>(device));
    }

    /// <summary>
    /// Deletes the device from the user
    /// </summary>
    /// <param name="deviceId"></param>
    /// <returns></returns>
    [HttpDelete]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteDevice(int deviceId)
    {
        if (deviceId <= 0) return BadRequest(await localizationService.TranslateAsync(UserId, "device-doesnt-exist"));

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Devices);
        if (user == null) return Unauthorized();

        if (await deviceService.Delete(user, deviceId)) return Ok();

        return BadRequest(await localizationService.TranslateAsync(UserId, "generic-device-delete"));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmailDeviceDto>>> GetDevices()
    {
        return Ok(await unitOfWork.DeviceRepository.GetDevicesForUserAsync(UserId));
    }

    /// <summary>
    /// Sends a collection of chapters to the user's device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("send-to")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> SendToDevice(SendToEmailDeviceDto dto)
    {
        var userId = UserId;
        if (dto.ChapterIds.Any(i => i < 0)) return BadRequest(await localizationService.TranslateAsync(userId, "greater-0", "ChapterIds"));
        if (dto.DeviceId < 0) return BadRequest(await localizationService.TranslateAsync(userId, "greater-0", "DeviceId"));

        var isEmailSetup = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).IsEmailSetupForSendToDevice();
        if (!isEmailSetup)
            return BadRequest(await localizationService.TranslateAsync(userId, "send-to-kavita-email"));

        // // Validate that the device belongs to the user
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.Devices);
        if (user == null || user.Devices.All(d => d.Id != dto.DeviceId)) return BadRequest(await localizationService.TranslateAsync(userId, "send-to-unallowed"));

        await eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
            MessageFactory.SendingToDeviceEvent(await localizationService.TranslateAsync(userId, "send-to-device-status"),
                "started"), userId);
        try
        {
            var success = await deviceService.SendTo(dto.ChapterIds, dto.DeviceId);
            if (success) return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(userId, ex.Message));
        }
        finally
        {
            await eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
                MessageFactory.SendingToDeviceEvent(await localizationService.TranslateAsync(userId, "send-to-device-status"),
                    "ended"), userId);
        }

        return BadRequest(await localizationService.TranslateAsync(userId, "generic-send-to"));
    }


    /// <summary>
    /// Attempts to send a whole series to a device.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("send-series-to")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> SendSeriesToDevice(SendSeriesToEmailDeviceDto dto)
    {
        var userId = UserId;
        if (dto.SeriesId <= 0) return BadRequest(await localizationService.TranslateAsync(userId, "greater-0", "SeriesId"));
        if (dto.DeviceId < 0) return BadRequest(await localizationService.TranslateAsync(userId, "greater-0", "DeviceId"));

        var isEmailSetup = (await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).IsEmailSetupForSendToDevice();
        if (!isEmailSetup)
            return BadRequest(await localizationService.TranslateAsync(userId, "send-to-kavita-email"));

        await eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
            MessageFactory.SendingToDeviceEvent(await localizationService.TranslateAsync(userId, "send-to-device-status"),
                "started"), userId);

        var series =
            await unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId,
                SeriesIncludes.Volumes | SeriesIncludes.Chapters);
        if (series == null) return BadRequest(await localizationService.TranslateAsync(userId, "series-doesnt-exist"));
        var chapterIds = series.Volumes.SelectMany(v => v.Chapters.Select(c => c.Id)).ToList();
        try
        {
            var success = await deviceService.SendTo(chapterIds, dto.DeviceId);
            if (success) return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(await localizationService.TranslateAsync(userId, ex.Message));
        }
        finally
        {
            await eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
                MessageFactory.SendingToDeviceEvent(await localizationService.TranslateAsync(userId, "send-to-device-status"),
                    "ended"), userId);
        }

        return BadRequest(await localizationService.TranslateAsync(userId, "generic-send-to"));
    }

    #region Client Devices
    /// <summary>
    /// Get my client devices
    /// </summary>
    /// <param name="includeInactive"></param>
    /// <returns></returns>
    [HttpGet("client/devices")]
    public async Task<ActionResult<List<ClientDeviceDto>>> GetMyClientDevices(bool includeInactive = false)
    {
        return Ok(await unitOfWork.ClientDeviceRepository.GetUserDeviceDtosAsync(UserId,  includeInactive));
    }

    /// <summary>
    /// Get All user client devices
    /// </summary>
    /// <param name="includeInactive"></param>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpGet("client/all-devices")]
    public async Task<ActionResult<List<ClientDeviceDto>>> GetAllClientDevices(bool includeInactive = false)
    {
        return Ok(await unitOfWork.ClientDeviceRepository.GetAllUserDeviceDtos(includeInactive));
    }


    /// <summary>
    /// Removes the client device from DB
    /// </summary>
    /// <param name="clientDeviceId"></param>
    /// <returns></returns>
    [HttpDelete("client/device")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<bool>> DeleteClientDevice(int clientDeviceId)
    {
        return Ok(await clientDeviceService.DeleteDeviceAsync(UserId, clientDeviceId));
    }

    /// <summary>
    /// Update the friendly name of the Device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("client/update-name")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> UpdateClientDeviceName(UpdateClientDeviceNameDto dto)
    {
        await clientDeviceService.UpdateFriendlyNameAsync(UserId, dto);
        return Ok();
    }



    #endregion Client Devices

}


