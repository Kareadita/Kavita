using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Device;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ng.Services;

namespace API.Controllers;

/// <summary>
/// Responsible interacting and creating Devices
/// </summary>
public class DeviceController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeviceService _deviceService;
    private readonly IUserAgentService _userAgentService;

    public DeviceController(IUnitOfWork unitOfWork, IDeviceService deviceService, IUserAgentService userAgentService)
    {
        _unitOfWork = unitOfWork;
        _deviceService = deviceService;
        _userAgentService = userAgentService;
    }

    [HttpPost("create-web")]
    public async Task<ActionResult> CreateOrUpdateDevice()
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        var details = _userAgentService.Parse(Request.Headers.UserAgent);
        //var device = await _deviceService.CreateDevice(details.Browser);
        var device = DbFactory.Device(details.Browser);

        // I can use JWT to tie with the User
        device.Platform = details.Platform;
        device.Version = details.BrowserVersion;
        device.IsBrowser = details.IsBrowser;
        device.IsMobile = details.IsMobile;
        device.IsRobot = details.IsRobot;
        device.IsManaged = false;
        if (Request.HttpContext.Connection.RemoteIpAddress != null)
            device.IpAddress = Request.HttpContext.Connection.RemoteIpAddress.ToString();


        return Ok();
    }

    [HttpPost("create")]
    public async Task<ActionResult> CreateOrUpdateDevice(CreateDeviceDto dto)
    {
        var device = await _deviceService.CreateDevice(dto.Name);
        return Ok();
    }





}


