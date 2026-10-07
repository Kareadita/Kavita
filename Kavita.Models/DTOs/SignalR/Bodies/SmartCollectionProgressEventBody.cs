using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public record SmartCollectionProgressEventBody(string CollectionName, float Progress, DateTime EventTime);
