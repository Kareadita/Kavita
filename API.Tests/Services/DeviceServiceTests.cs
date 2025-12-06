using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Device;
using API.DTOs.Device.EmailDevice;
using API.Entities;
using API.Entities.Enums.Device;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class DeviceServiceDbTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private readonly ILogger<DeviceService> _logger = Substitute.For<ILogger<DeviceService>>();

    private Task<IDeviceService> Setup(IUnitOfWork unitOfWork)
    {
        return Task.FromResult<IDeviceService>(new DeviceService(unitOfWork, _logger, Substitute.For<IEmailService>()));
    }

    [Fact]
    public async Task CreateDevice_Succeeds()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var deviceService = await Setup(unitOfWork);

        var user = new AppUser()
        {
            UserName = "majora2007",
            Devices = new List<Device>()
        };

        context.Users.Add(user);
        await unitOfWork.CommitAsync();

        var device = await deviceService.Create(new CreateEmailDeviceDto()
        {
            EmailAddress = "fake@kindle.com",
            Name = "Test Kindle",
            Platform = EmailDevicePlatform.Kindle
        }, user);

        Assert.NotNull(device);
    }

    [Fact]
    public async Task CreateDevice_ThrowsErrorWhenEmailDoesntMatchRules()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var deviceService = await Setup(unitOfWork);

        var user = new AppUser()
        {
            UserName = "majora2007",
            Devices = new List<Device>()
        };

        context.Users.Add(user);
        await unitOfWork.CommitAsync();

        var device = await deviceService.Create(new CreateEmailDeviceDto()
        {
            EmailAddress = "fake@gmail.com",
            Name = "Test Kindle",
            Platform = EmailDevicePlatform.Kindle
        }, user);

        Assert.NotNull(device);

    }
}
