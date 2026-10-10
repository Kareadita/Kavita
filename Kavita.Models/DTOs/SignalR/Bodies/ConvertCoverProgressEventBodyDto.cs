using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ConvertCoverProgressEventBodyDto(float Progress, DateTime EventTime);
