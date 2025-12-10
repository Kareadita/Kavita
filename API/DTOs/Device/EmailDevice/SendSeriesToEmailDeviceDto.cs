namespace API.DTOs.Device.EmailDevice;

public sealed record SendSeriesToEmailDeviceDto
{
    public int DeviceId { get; set; }
    public int SeriesId { get; set; }
}
