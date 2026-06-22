using System;
using System.ComponentModel;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public enum KavitaPlusProviderHealthStatus
{
    [Description("Unknown")]
    Unknown = 0,
    [Description("Operational")]
    Operational = 1,
    [Description("Degraded")]
    Degraded = 2,
    [Description("Down")]
    Down = 3,
}

public enum KavitaPlusProviderHealthIncidentType
{
    [Description("Degraded")]
    Degraded = 1,
    [Description("Down")]
    Down = 2,
}

public sealed record KavitaPlusProviderHealthSnapshotDto
{
    public ScrobbleProvider Provider { get; set; }
    public double AvgLatencyMs { get; set; }
    public KavitaPlusProviderHealthStatus Status { get; set; }
    public KavitaPlusProviderIncidentDto? LastIncident { get; set; }
}

public sealed record KavitaPlusProviderIncidentDto
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public KavitaPlusProviderHealthIncidentType Type { get; set; }
}
