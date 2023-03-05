using System.Collections.Generic;

namespace API.DTOs.Device;

public class SendToDeviceDto
{
    public int DeviceId { get; set; }
    public IReadOnlyList<int> ChapterIds { get; set; } = default!;
}
