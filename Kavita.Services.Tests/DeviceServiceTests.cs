using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.Database.Tests;
using Kavita.Models.DTOs.Device.EmailDevice;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums.Device;
using Kavita.Models.Entities.User;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class DeviceServiceDbTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private readonly ILogger<DeviceService> _logger = Substitute.For<ILogger<DeviceService>>();

    private Task<IDeviceService> Setup(IUnitOfWork unitOfWork)
    {
        return Task.FromResult<IDeviceService>(new DeviceService(unitOfWork, _logger,
            Substitute.For<IEmailService>(), Substitute.For<IReadingProfileService>()));
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
