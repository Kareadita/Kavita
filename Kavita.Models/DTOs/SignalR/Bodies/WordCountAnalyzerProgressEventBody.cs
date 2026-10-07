using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public record WordCountAnalyzerProgressEventBody(int LibraryId, float Progress, DateTime EventTime);
