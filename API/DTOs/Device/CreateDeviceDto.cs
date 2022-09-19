using System.Runtime.InteropServices;
using API.Entities.Enums.Device;

namespace API.DTOs.Device;

public class CreateDeviceDto
{
    public string Name { get; set; }
    /// <summary>
    /// Platform of the device. If not know, defaults to "Custom"
    /// </summary>
    public DevicePlatform Platform { get; set; }
    public string EmailAddress { get; set; }


}
