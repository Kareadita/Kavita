namespace API.DTOs.Device;

public sealed record SendSeriesToDeviceDto
{
    public int DeviceId { get; set; }
    public int SeriesId { get; set; }
}
