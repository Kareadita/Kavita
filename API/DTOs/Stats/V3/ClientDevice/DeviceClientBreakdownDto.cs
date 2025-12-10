using System.Collections.Generic;
using API.DTOs.Statistics;
using API.Entities.Enums;

namespace API.DTOs.Stats.V3.ClientDevice;

public sealed record DeviceClientBreakdownDto
{
    public IList<StatCount<ClientDeviceType>> Records { get; set; }
    public int TotalCount { get; set; }
}
