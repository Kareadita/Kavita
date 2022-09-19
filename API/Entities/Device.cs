using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using API.Entities.Interfaces;

namespace API.Entities;

/// <summary>
/// A Device is an entity that interacts with Kavita via web UI (browser) or that can receive data from Kavita (kindle).
/// When native apps are built, they will also be represented with a device.
/// </summary>
public class Device : IEntityDate
{
    public int Id { get; set; }
    /// <summary>
    /// Last Seen IP Address of the device
    /// </summary>
    public string IpAddress { get; set; }
    /// <summary>
    /// A name given to this device
    /// </summary>
    /// <remarks>If this device is web, this will be the browser name</remarks>
    /// <example>Pixel 3a, John's Kindle</example>
    public string Name { get; set; }
    /// <summary>
    /// Is this device a browser
    /// </summary>
    public bool IsBrowser { get; set; }
    public bool IsMobile { get; set; }
    public bool IsRobot { get; set; }
    /// <summary>
    /// If this is a user created device like a Kindle or Pocketbook. This would indicate the Name is custom and there is likely an Email associated
    /// </summary>
    public bool IsManaged { get; set; }
    /// <summary>
    /// Version of the Browser
    /// </summary>
    public string Version { get; set; }


    /// <summary>
    /// An email address associated with the device (ie Kindle). Will be used with Send to functionality
    /// </summary>
    public string EmailAddress { get; set; }
    /// <summary>
    /// Platform (ie) Windows 10
    /// </summary>
    public string Platform { get; set; }


    //public ICollection<string> SupportedExtensions { get; set; } // TODO: This requires some sort of information at mangaFile level (unless i repack)

    // We need a way to track that someone is using this device and connected maybe (is that possible)?

    //public ICollection<AppUser> Owners { get; set; }


    /// <summary>
    /// Last time this device interacted with Kavita or a file was sent to it
    /// </summary>
    public DateTime LastSeen { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
}
