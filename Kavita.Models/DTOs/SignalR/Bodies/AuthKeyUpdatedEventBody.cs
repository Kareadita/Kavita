using Kavita.Models.DTOs.Account;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public record AuthKeyUpdatedEventBody(AuthKeyDto AuthKey);
public record AuthKeyDeletedEventBody(int Id);
