using System.Collections.Generic;
using Kavita.Models.DTOs.Statistics;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Stats.V3.ClientDevice;

public sealed record DeviceClientBreakdownDto
{
    public IList<StatCount<ClientDeviceType>> Records { get; set; }
    public int TotalCount { get; set; }
}
