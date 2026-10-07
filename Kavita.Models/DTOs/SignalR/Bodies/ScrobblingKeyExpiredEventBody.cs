using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public record ScrobblingKeyExpiredEventBody(ScrobbleProvider Provider);
public record ScrobbleProviderUpdatedEventBody(ScrobbleProvider Provider);
