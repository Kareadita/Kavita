using Kavita.Models.DTOs.Settings;

namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record KepubifyCheckedEventBodyDto(string Name, string Title, string SubTitle, KepubifyStatusDto Status);
