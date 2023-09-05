﻿using API.DTOs.Filtering.v2;
using API.Entities.Enums;

namespace API.DTOs.Account;

public class DashboardStreamDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>
    /// Is System Provided
    /// </summary>
    public bool IsProvided { get; set; }
    /// <summary>
    /// Sort Order on the Dashboard
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// If Not IsProvided, the appropriate smart filter
    /// </summary>
    /// <remarks>Encoded filter</remarks>
    public string? SmartFilterEncoded { get; set; }
    public FilterV2Dto? SmartFilter { get; set; }
    /// <summary>
    /// For system provided
    /// </summary>
    public DashboardStreamType StreamType { get; set; }
}
