using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ConvertBookmarksProgressEventBody(float Progress, DateTime EventTime);
public sealed record ConvertCoverProgressEventBody(float Progress, DateTime EventTime);
