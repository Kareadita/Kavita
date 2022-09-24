﻿using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using API.Entities.Enums.Device;

namespace API.DTOs.Device;

public class CreateDeviceDto
{
    [Required]
    public string Name { get; set; }
    /// <summary>
    /// Platform of the device. If not know, defaults to "Custom"
    /// </summary>
    [Required]
    public DevicePlatform Platform { get; set; }
    [Required]
    public string EmailAddress { get; set; }


}
