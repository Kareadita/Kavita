using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public record CoverUpdateProgressEventBody(int LibraryId, float Progress, DateTime EventTime);
