using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Device;
using API.DTOs.Device.ClientDevice;
using API.DTOs.Device.EmailDevice;
using API.DTOs.Progress;
using API.Services;
using API.SignalR;
using AutoMapper;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

/// <summary>
/// Responsible interacting and creating Devices
/// </summary>
public class DeviceController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeviceService _deviceService;
    private readonly IEventHub _eventHub;
    private readonly ILocalizationService _localizationService;
    private readonly IMapper _mapper;
    private readonly IClientDeviceService _clientDeviceService;

    public DeviceController(IUnitOfWork unitOfWork, IDeviceService deviceService,IEventHub eventHub,
        ILocalizationService localizationService, IMapper mapper, IClientDeviceService clientDeviceService)
    {
        _unitOfWork = unitOfWork;
        _deviceService = deviceService;
        _eventHub = eventHub;
        _localizationService = localizationService;
        _mapper = mapper;
        _clientDeviceService = clientDeviceService;
    }


    /// <summary>
    /// Creates a new Device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<ActionResult<EmailDeviceDto>> CreateOrUpdateDevice(CreateEmailDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        try
        {
            var device = await _deviceService.Create(dto, user);
            if (device == null)
                return BadRequest(await _localizationService.Translate(UserId, "generic-device-create"));

            return Ok(_mapper.Map<EmailDeviceDto>(device));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(UserId, ex.Message));
        }
    }

    /// <summary>
    /// Updates an existing Device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<ActionResult<EmailDeviceDto>> UpdateDevice(UpdateEmailDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        var device = await _deviceService.Update(dto, user);

        if (device == null) return BadRequest(await _localizationService.Translate(UserId, "generic-device-update"));

        return Ok(_mapper.Map<EmailDeviceDto>(device));
    }

    /// <summary>
    /// Deletes the device from the user
    /// </summary>
    /// <param name="deviceId"></param>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteDevice(int deviceId)
    {
        if (deviceId <= 0) return BadRequest(await _localizationService.Translate(UserId, "device-doesnt-exist"));
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(Username!, AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        if (await _deviceService.Delete(user, deviceId)) return Ok();

        return BadRequest(await _localizationService.Translate(UserId, "generic-device-delete"));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmailDeviceDto>>> GetDevices()
    {
        return Ok(await _unitOfWork.DeviceRepository.GetDevicesForUserAsync(UserId));
    }

    /// <summary>
    /// Sends a collection of chapters to the user's device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("send-to")]
    public async Task<ActionResult> SendToDevice(SendToEmailDeviceDto dto)
    {
        var userId = UserId;
        if (dto.ChapterIds.Any(i => i < 0)) return BadRequest(await _localizationService.Translate(userId, "greater-0", "ChapterIds"));
        if (dto.DeviceId < 0) return BadRequest(await _localizationService.Translate(userId, "greater-0", "DeviceId"));

        var isEmailSetup = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).IsEmailSetupForSendToDevice();
        if (!isEmailSetup)
            return BadRequest(await _localizationService.Translate(userId, "send-to-kavita-email"));

        // // Validate that the device belongs to the user
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.Devices);
        if (user == null || user.Devices.All(d => d.Id != dto.DeviceId)) return BadRequest(await _localizationService.Translate(userId, "send-to-unallowed"));

        await _eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
            MessageFactory.SendingToDeviceEvent(await _localizationService.Translate(userId, "send-to-device-status"),
                "started"), userId);
        try
        {
            var success = await _deviceService.SendTo(dto.ChapterIds, dto.DeviceId);
            if (success) return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(userId, ex.Message));
        }
        finally
        {
            await _eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
                MessageFactory.SendingToDeviceEvent(await _localizationService.Translate(userId, "send-to-device-status"),
                    "ended"), userId);
        }

        return BadRequest(await _localizationService.Translate(userId, "generic-send-to"));
    }


    /// <summary>
    /// Attempts to send a whole series to a device.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("send-series-to")]
    public async Task<ActionResult> SendSeriesToDevice(SendSeriesToEmailDeviceDto dto)
    {
        var userId = UserId;
        if (dto.SeriesId <= 0) return BadRequest(await _localizationService.Translate(userId, "greater-0", "SeriesId"));
        if (dto.DeviceId < 0) return BadRequest(await _localizationService.Translate(userId, "greater-0", "DeviceId"));

        var isEmailSetup = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).IsEmailSetupForSendToDevice();
        if (!isEmailSetup)
            return BadRequest(await _localizationService.Translate(userId, "send-to-kavita-email"));

        await _eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
            MessageFactory.SendingToDeviceEvent(await _localizationService.Translate(userId, "send-to-device-status"),
                "started"), userId);

        var series =
            await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId,
                SeriesIncludes.Volumes | SeriesIncludes.Chapters);
        if (series == null) return BadRequest(await _localizationService.Translate(userId, "series-doesnt-exist"));
        var chapterIds = series.Volumes.SelectMany(v => v.Chapters.Select(c => c.Id)).ToList();
        try
        {
            var success = await _deviceService.SendTo(chapterIds, dto.DeviceId);
            if (success) return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(userId, ex.Message));
        }
        finally
        {
            await _eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
                MessageFactory.SendingToDeviceEvent(await _localizationService.Translate(userId, "send-to-device-status"),
                    "ended"), userId);
        }

        return BadRequest(await _localizationService.Translate(userId, "generic-send-to"));
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
        return Ok(await _clientDeviceService.GetUserDeviceDtosAsync(UserId,  includeInactive));
    }

    /// <summary>
    /// Get All user client devices
    /// </summary>
    /// <param name="includeInactive"></param>
    /// <returns></returns>
    [HttpGet("client/all-devices")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<List<ClientDeviceDto>>> GetAllClientDevices(bool includeInactive = false)
    {
        return Ok(await _clientDeviceService.GetAllUserDeviceDtos(includeInactive));
    }


    /// <summary>
    /// Removes the client device from DB
    /// </summary>
    /// <param name="clientDeviceId"></param>
    /// <returns></returns>
    [HttpDelete("client/device")]
    public async Task<ActionResult<bool>> DeleteClientDevice(int clientDeviceId)
    {
        return Ok(await _clientDeviceService.DeleteDeviceAsync(UserId, clientDeviceId));
    }

    /// <summary>
    /// Update the friendly name of the Device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("client/update-name")]
    public async Task<ActionResult> UpdateClientDeviceName(UpdateClientDeviceNameDto dto)
    {
        await _clientDeviceService.UpdateFriendlyNameAsync(UserId, dto);
        return Ok();
    }



    #endregion Client Devices

}


