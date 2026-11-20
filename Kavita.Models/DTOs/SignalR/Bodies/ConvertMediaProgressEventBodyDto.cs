using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;
#nullable enable

/// <param name="Current">Images done in the current stage with <paramref name="Total"/> the activity row shows "current / total"</param>
public sealed record ConvertMediaProgressEventBodyDto(float Progress, DateTime EventTime, int? Current = null, int? Total = null);
