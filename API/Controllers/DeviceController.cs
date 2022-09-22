using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Device;
using API.Extensions;
using API.Services;
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

    public DeviceController(IUnitOfWork unitOfWork, IDeviceService deviceService)
    {
        _unitOfWork = unitOfWork;
        _deviceService = deviceService;
    }


    [HttpPost("create")]
    public async Task<ActionResult> CreateOrUpdateDevice(CreateDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        var device = await _deviceService.Create(dto, user);

        if (device == null) return BadRequest("There was an error when creating the device");

        return Ok();
    }

    [HttpPost("edit")]
    public async Task<ActionResult> UpdateDevice(UpdateDeviceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername(), AppUserIncludes.Devices);
        var device = await _deviceService.Update(dto, user);

        if (device == null) return BadRequest("There was an error when updating the device");

        return Ok(device);
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





}


