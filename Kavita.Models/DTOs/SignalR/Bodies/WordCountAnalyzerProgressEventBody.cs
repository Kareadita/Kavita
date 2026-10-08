using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record WordCountAnalyzerProgressEventBody(int LibraryId, float Progress, DateTime EventTime);
