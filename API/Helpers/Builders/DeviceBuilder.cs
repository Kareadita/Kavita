using API.Entities;
using API.Entities.Enums.Device;

namespace API.Helpers.Builders;

public class DeviceBuilder : IEntityBuilder<Device>
{
    private readonly Device _device;
    public Device Build() => _device;

    public DeviceBuilder(string name)
    {
        _device = new Device()
        {
            Name = name,
            Platform = DevicePlatform.Custom
        };
    }

    public DeviceBuilder WithPlatform(DevicePlatform platform)
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
