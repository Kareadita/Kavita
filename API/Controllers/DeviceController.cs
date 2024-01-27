using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Device;
using API.Extensions;
using API.Services;
using API.SignalR;
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

    public DeviceController(IUnitOfWork unitOfWork, IDeviceService deviceService,
        IEmailService emailService, IEventHub eventHub, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _deviceService = deviceService;
        _emailService = emailService;
        _eventHub = eventHub;
        _localizationService = localizationService;
    }


    [HttpPost("create")]
    public async Task<ActionResult> CreateOrUpdateDevice(CreateDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        try
        {
            var device = await _deviceService.Create(dto, user);
            if (device == null)
                return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-device-create"));
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }




        return Ok();
    }

    [HttpPost("update")]
    public async Task<ActionResult> UpdateDevice(UpdateDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        if (user == null) return Unauthorized();
        var device = await _deviceService.Update(dto, user);

        if (device == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-device-update"));

        return Ok();
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
        if (dto.ChapterIds.Any(i => i < 0)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "greater-0", "ChapterIds"));
        if (dto.DeviceId < 0) return BadRequest(await _localizationService.Translate(User.GetUserId(), "greater-0", "DeviceId"));

        var isEmailSetup = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).IsEmailSetup();
        if (!isEmailSetup)
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "send-to-kavita-email"));

        // // Validate that the device belongs to the user
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.Devices);
        if (user == null || user.Devices.All(d => d.Id != dto.DeviceId)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "send-to-unallowed"));

        var userId = User.GetUserId();
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



    [HttpPost("send-series-to")]
    public async Task<ActionResult> SendSeriesToDevice(SendSeriesToDeviceDto dto)
    {
        if (dto.SeriesId <= 0) return BadRequest(await _localizationService.Translate(User.GetUserId(), "greater-0", "SeriesId"));
        if (dto.DeviceId < 0) return BadRequest(await _localizationService.Translate(User.GetUserId(), "greater-0", "DeviceId"));

        var isEmailSetup = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).IsEmailSetup();
        if (!isEmailSetup)
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "send-to-kavita-email"));

        var userId = User.GetUserId();
        await _eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
            MessageFactory.SendingToDeviceEvent(await _localizationService.Translate(User.GetUserId(), "send-to-device-status"),
                "started"), userId);

        var series =
            await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId,
                SeriesIncludes.Volumes | SeriesIncludes.Chapters);
        if (series == null) return BadRequest(await _localizationService.Translate(User.GetUserId(), "series-doesnt-exist"));
        var chapterIds = series.Volumes.SelectMany(v => v.Chapters.Select(c => c.Id)).ToList();
        try
        {
            var success = await _deviceService.SendTo(chapterIds, dto.DeviceId);
            if (success) return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), ex.Message));
        }
        finally
        {
            await _eventHub.SendMessageToAsync(MessageFactory.NotificationProgress,
                MessageFactory.SendingToDeviceEvent(await _localizationService.Translate(User.GetUserId(), "send-to-device-status"),
                    "ended"), userId);
        }

        return BadRequest(await _localizationService.Translate(User.GetUserId(), "generic-send-to"));
    }

}


