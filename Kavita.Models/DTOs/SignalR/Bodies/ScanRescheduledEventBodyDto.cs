using System.Collections.Generic;

namespace Kavita.Models.DTOs.SignalR.Bodies;

/// <param name="Scans">Every delayed scan, in run order</param>
public sealed record ScanRescheduledEventBodyDto(IList<ScheduledScanDto> Scans);
