using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public record ConvertBookmarksProgressEventBody(float Progress, DateTime EventTime);
public record ConvertCoverProgressEventBody(float Progress, DateTime EventTime);
