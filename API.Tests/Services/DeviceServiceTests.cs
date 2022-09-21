using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Device;
using API.Entities;
using API.Entities.Enums.Device;
using API.Services;
using API.Services.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class DeviceServiceTests : BasicTest
{
    private readonly ILogger<DeviceService> _logger = Substitute.For<ILogger<DeviceService>>();
    private readonly IDeviceService _deviceService;

    public DeviceServiceTests() : base()
    {
        _deviceService = new DeviceService(_unitOfWork, _logger);
    }

    protected void ResetDB()
    {
        _context.Users.RemoveRange(_context.Users.ToList());
    }



    [Fact]
    public async Task CreateDevice_Succeeds()
    {

        var user = new AppUser()
        {
            UserName = "majora2007",
            Devices = new List<Device>()
        };

        _context.Users.Add(user);
        await _unitOfWork.CommitAsync();

        var device = await _deviceService.Create(new CreateDeviceDto()
        {
            EmailAddress = "fake@kindle.com",
            Name = "Test Kindle",
            Platform = DevicePlatform.Kindle
        }, user);

        Assert.NotNull(device);

    }

    [Fact]
    public async Task CreateDevice_ThrowsErrorWhenEmailDoesntMatchRules()
    {

        var user = new AppUser()
        {
            UserName = "majora2007",
            Devices = new List<Device>()
        };

        _context.Users.Add(user);
        await _unitOfWork.CommitAsync();

        var device = await _deviceService.Create(new CreateDeviceDto()
        {
            EmailAddress = "fake@gmail.com",
            Name = "Test Kindle",
            Platform = DevicePlatform.Kindle
        }, user);

        Assert.NotNull(device);

    }
}
