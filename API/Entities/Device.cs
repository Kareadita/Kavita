using System;
using API.Entities.Enums.Device;
using API.Entities.Interfaces;

namespace API.Entities;

/// <summary>
/// A Device is an entity that can receive data from Kavita (kindle)
/// </summary>
public class Device : IEntityDate
{
    public int Id { get; set; }
    /// <summary>
    /// Last Seen IP Address of the device
    /// </summary>
    public string? IpAddress { get; set; }
    /// <summary>
    /// A name given to this device
    /// </summary>
    /// <remarks>If this device is web, this will be the browser name</remarks>
    /// <example>Pixel 3a, John's Kindle</example>
    public string? Name { get; set; }
    /// <summary>
    /// An email address associated with the device (ie Kindle). Will be used with Send to functionality
    /// </summary>
    public string? EmailAddress { get; set; }
    /// <summary>
    /// Platform (ie) Windows 10
    /// </summary>
    public DevicePlatform Platform { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;


    /// <summary>
    /// Last time this device was used to send a file
    /// </summary>
    public DateTime LastUsed { get; set; }
    public DateTime LastUsedUtc { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    public void UpdateLastUsed()
    {
        LastUsed = DateTime.Now;
        LastUsedUtc = DateTime.UtcNow;
    }
}
