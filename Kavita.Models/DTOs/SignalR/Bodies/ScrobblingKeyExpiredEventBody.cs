using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record ScrobblingKeyExpiredEventBody(ScrobbleProvider Provider);
public sealed record ScrobbleProviderUpdatedEventBody(ScrobbleProvider Provider);
