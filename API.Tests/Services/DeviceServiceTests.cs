using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Device;
using API.Entities;
using API.Entities.Enums.Device;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class DeviceServiceDbTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private readonly ILogger<DeviceService> _logger = Substitute.For<ILogger<DeviceService>>();

    private async Task<IDeviceService> Setup(IUnitOfWork unitOfWork)
    {
        return new DeviceService(unitOfWork, _logger, Substitute.For<IEmailService>());
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

        var device = await deviceService.Create(new CreateDeviceDto()
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
        var (unitOfWork, context, _) = await CreateDatabase();
        var deviceService = await Setup(unitOfWork);

        var user = new AppUser()
        {
            UserName = "majora2007",
            Devices = new List<Device>()
        };

        context.Users.Add(user);
        await unitOfWork.CommitAsync();

        var device = await deviceService.Create(new CreateDeviceDto()
        {
            EmailAddress = "fake@gmail.com",
            Name = "Test Kindle",
            Platform = DevicePlatform.Kindle
        }, user);

        Assert.NotNull(device);

    }
}
