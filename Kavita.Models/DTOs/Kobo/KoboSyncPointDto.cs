using System;
using Kavita.Models.DTOs.Progress;

namespace Kavita.Models.DTOs.Kobo;

public class KoboSyncPointDto
{
    public int Id { get; set; }
    public string DeviceId { get; set; }
    public ClientDeviceDto? ClientDevice { get; set; }
    public int RemovedChapterCount { get; set; }
    public DateTime LastSync { get; set; }
}
