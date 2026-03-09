using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums.Device;

namespace Kavita.Models.Builders;

public class DeviceBuilder : IEntityBuilder<Device>
{
    private readonly Device _device;
    public Device Build() => _device;

    public DeviceBuilder(string name)
    {
        _device = new Device()
        {
            Name = name,
            Platform = EmailDevicePlatform.Custom
        };
    }

    public DeviceBuilder WithPlatform(EmailDevicePlatform platform)
    {
        _device.Platform = platform;
        return this;
    }
    public DeviceBuilder WithEmail(string email)
    {
        _device.EmailAddress = email;
        return this;
    }
}
