using System.Collections.Generic;

namespace API.DTOs.Device;

public sealed record SendToDeviceDto
{
    public int DeviceId { get; set; }
    public IReadOnlyList<int> ChapterIds { get; set; } = default!;
}
