using System;
using API.Entities.Enums.Device;

namespace API.DTOs.Device;

/// <summary>
/// A Device is an entity that can receive data from Kavita (kindle)
/// </summary>
public class DeviceDto
{
    /// <summary>
    /// The device Id
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// A name given to this device
    /// </summary>
    /// <remarks>If this device is web, this will be the browser name</remarks>
    /// <example>Pixel 3a, John's Kindle</example>
    public string Name { get; set; } = default!;
    /// <summary>
    /// An email address associated with the device (ie Kindle). Will be used with Send to functionality
    /// </summary>
    public string EmailAddress { get; set; } = default!;
    /// <summary>
    /// Platform (ie) Windows 10
    /// </summary>
    public DevicePlatform Platform { get; set; }
}
