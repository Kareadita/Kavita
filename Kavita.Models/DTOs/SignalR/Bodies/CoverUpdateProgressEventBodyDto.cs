using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record CoverUpdateProgressEventBodyDto(int LibraryId, float Progress, DateTime EventTime);
