using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Device;
using API.Extensions;
using API.Services;
using API.SignalR;
using AutoMapper;
using Kavita.Common;
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
    private readonly IEmailService _emailService;
    private readonly IEventHub _eventHub;
    private readonly ILocalizationService _localizationService;
    private readonly IMapper _mapper;

    public DeviceController(IUnitOfWork unitOfWork, IDeviceService deviceService,
        IEmailService emailService, IEventHub eventHub, ILocalizationService localizationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _deviceService = deviceService;
        _emailService = emailService;
        _eventHub = eventHub;
        _localizationService = localizationService;
        _mapper = mapper;
    }


    /// <summary>
    /// Creates a new Device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("create")]
    public async Task<ActionResult<DeviceDto>> CreateOrUpdateDevice(CreateDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        try
        {
            var device = await _deviceService.Create(dto, user);
            if (device == null)
                return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-device-create"));

            return Ok(_mapper.Map<DeviceDto>(device));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }
    }

    /// <summary>
    /// Updates an existing Device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<ActionResult<DeviceDto>> UpdateDevice(UpdateDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        var device = await _deviceService.Update(dto, user);

        if (device == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-device-update"));

        return Ok(_mapper.Map<DeviceDto>(device));
    }

    /// <summary>
    /// Deletes the device from the user
    /// </summary>
    /// <param name="deviceId"></param>
    /// <returns></returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteDevice(int deviceId)
    {
        if (deviceId <= 0) return BadRequest(await _localizationService.Translate(User.GetUserId(), "device-doesnt-exist"));
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        if (await _deviceService.Delete(user, deviceId)) return Ok();

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-device-delete"));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceDto>>> GetDevices()
    {
        return Ok(await _unitOfWork.DeviceRepository.GetDevicesForUserAsync(User.GetUserId()));
    }

    /// <summary>
    /// Sends a collection of chapters to the user's device
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("send-to")]
    public async Task<ActionResult> SendToDevice(SendToDeviceDto dto)
    {
        var userId = User.GetUserId();
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
    public async Task<ActionResult> SendSeriesToDevice(SendSeriesToDeviceDto dto)
    {
        var userId = User.GetUserId();
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

}


