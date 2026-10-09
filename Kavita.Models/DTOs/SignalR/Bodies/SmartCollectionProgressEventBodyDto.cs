using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record SmartCollectionProgressEventBodyDto(string CollectionName, float Progress, DateTime EventTime);
