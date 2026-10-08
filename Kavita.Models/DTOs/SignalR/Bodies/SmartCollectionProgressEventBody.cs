using System;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record SmartCollectionProgressEventBody(string CollectionName, float Progress, DateTime EventTime);
