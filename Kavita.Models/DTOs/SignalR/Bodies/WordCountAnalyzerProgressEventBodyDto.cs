using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record WordCountAnalyzerProgressEventBodyDto(int LibraryId, float Progress, DateTime EventTime);
