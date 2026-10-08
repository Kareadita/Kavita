using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record CoverUpdateProgressEventBody(int LibraryId, float Progress, DateTime EventTime);
