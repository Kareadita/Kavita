using System.ComponentModel.DataAnnotations;
using API.Entities.Enums.Device;

namespace API.DTOs.Device.EmailDevice;

public sealed record UpdateEmailDeviceDto
{
    [Required]
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = default!;
    /// <summary>
    /// Platform of the device. If not know, defaults to "Custom"
    /// </summary>
    [Required]
    public EmailDevicePlatform Platform { get; set; }
    [Required]
    public string EmailAddress { get; set; } = default!;
}
