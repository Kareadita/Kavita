using Kavita.Models.DTOs.Account;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record AuthKeyUpdatedEventBody(AuthKeyDto AuthKey);
public sealed record AuthKeyDeletedEventBody(int Id);
