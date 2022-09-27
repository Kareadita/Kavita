using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Device;
using API.Extensions;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Responsible interacting and creating Devices
/// </summary>
public class DeviceController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeviceService _deviceService;
    private readonly IEmailService _emailService;

    public DeviceController(IUnitOfWork unitOfWork, IDeviceService deviceService, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _deviceService = deviceService;
        _emailService = emailService;
    }


    [HttpPost("create")]
    public async Task<ActionResult> CreateOrUpdateDevice(CreateDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        var device = await _deviceService.Create(dto, user);

        if (device == null) return BadRequest("There was an error when creating the device");

        return Ok();
    }

    [HttpPost("update")]
    public async Task<ActionResult> UpdateDevice(UpdateDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        var device = await _deviceService.Update(dto, user);

        if (device == null) return BadRequest("There was an error when updating the device");

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
        if (deviceId <= 0) return BadRequest("Not a valid deviceId");
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        if (await _deviceService.Delete(user, deviceId)) return Ok();

        return BadRequest("Could not delete device");
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceDto>>> GetDevices()
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.DeviceRepository.GetDevicesForUserAsync(userId));
    }

    [HttpPost("send-to")]
    public async Task<ActionResult> SendToDevice(SendToDeviceDto dto)
    {
        if (dto.ChapterIds.Any(i => i < 0)) return BadRequest("ChapterIds must be greater than 0");
        if (dto.DeviceId < 0) return BadRequest("DeviceId must be greater than 0");

        if (await _emailService.IsDefaultEmailService())
            return BadRequest("Send to device cannot be used with Kavita's email service. Please configure your own.");

        try
        {
            var success = await _deviceService.SendTo(dto.ChapterIds, dto.DeviceId);
            if (success) return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }

        return BadRequest("There was an error sending the file to the device");
    }



}


