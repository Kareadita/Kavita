namespace API.DTOs.Device.ClientDevice;

public sealed record UpdateClientDeviceNameDto
{
    public int DeviceId { get; set; }
    public string Name { get; set; }
}
