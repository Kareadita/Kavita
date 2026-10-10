using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ConvertBookmarksProgressEventBodyDto(float Progress, DateTime EventTime);
